using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using NoTiming.Ui.Storage.Browser;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Snapshots;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NoTiming.Ui.Features.Core.Dashboard;

/// <summary>
/// The groups that were sent and are not answered for good (#645, ADR-0013). Each waits under its own id, kept on the device with its
/// account and Event, and is sent again until the server answers. A group that the server records releases its Snapshots from the
/// other groups that wait for the Event, so that none is recorded twice. The page's service decides what to send and what is selected;
/// this class keeps the groups and the durable store's rules for them.
/// </summary>
public sealed class WaitingGroups
{
    readonly IAccountSession _account;
    readonly IDurableRepository<DurableSnapshotGroup> _groups;
    readonly ISnapshotPublisher _publisher;
    readonly List<Entry> _entries = [];

    public WaitingGroups(
        IAccountSession account,
        IDurableRepository<DurableSnapshotGroup> groups,
        ISnapshotPublisher publisher
    )
    {
        _account = account;
        _groups = groups;
        _publisher = publisher;
    }

    /// <summary>Every group that waits to be sent again, in the order they were made.</summary>
    public IReadOnlyList<SnapshotGroup> Waiting => [.. _entries.Where(x => x.CanBeSentAgain).Select(x => x.Group)];

    /// <summary>Whether a group waits for the Event.</summary>
    public bool HasWaitingFor(Guid eventId)
    {
        return _entries.Any(x => x.CanBeSentAgain && x.EventId == eventId);
    }

    /// <summary>The ids of the groups that wait for the Event, in the order they were made.</summary>
    public IReadOnlyList<Guid> IdsWaitingFor(Guid eventId)
    {
        return [.. _entries.Where(x => x.CanBeSentAgain && x.EventId == eventId).Select(x => x.Group.Id)];
    }

    /// <summary>The group that waits under the id, with the Event it is of; none once it is answered.</summary>
    public (Guid EventId, SnapshotGroup Group)? Find(Guid id)
    {
        if (_entries.FirstOrDefault(x => x.CanBeSentAgain && x.Group.Id == id) is not { } entry)
        {
            return null;
        }

        return (entry.EventId, entry.Group);
    }

    /// <summary>
    /// The group a repeat of the same Snapshots as the same kind is sent as, waiting or refused (ADR-0013); none for a new gesture, which
    /// is made with new ids.
    /// </summary>
    public SnapshotGroup? RepeatOf(Guid eventId, IReadOnlyList<Snapshot> snapshots, SnapshotType snapshotType)
    {
        return _entries
            .FirstOrDefault(x => x.EventId == eventId && SnapshotGroupRules.IsSentAgain(x.Group, snapshotType, snapshots))
            ?.Group;
    }

    /// <summary>
    /// Asks the server for the group. The group is kept on the device under its own id until the server answers for good. A group that no
    /// answer came to, or whose answer is only for now, keeps waiting; a recorded one is no longer kept; a refused one is kept only for a
    /// repeat of the same Snapshots.
    /// </summary>
    public async Task<IReadOnlyList<SnapshotReceipt>> Send(Guid eventId, SnapshotGroup group)
    {
        try
        {
            var (receipts, final) = await PublishKept(eventId, group);
            Answered(eventId, group, receipts, final);
            return receipts;
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            Remember(eventId, group, canBeSentAgain: true);
            throw;
        }
    }

    /// <summary>
    /// The Snapshots that the server recorded are answered. A group that waits for the Event and holds one of them no longer sends it, so
    /// that it is not recorded twice; a group left with nothing is forgotten. A failure to change the kept copy is not a failure of the
    /// send: the next send of the group keeps it again.
    /// </summary>
    public async Task Release(Guid eventId, SnapshotGroup group, IReadOnlyList<SnapshotReceipt> receipts)
    {
        var recorded = group
            .Entries.Where(x => SnapshotReceipts.IsRecorded(receipts, group.IdOf(x)))
            .Select(x => x.Number)
            .ToHashSet();
        if (recorded.Count == 0)
        {
            return;
        }

        var others = _entries
            .Where(x =>
                x.CanBeSentAgain
                && x.EventId == eventId
                && x.Group.Id != group.Id
                && x.Group.Entries.Any(entry => recorded.Contains(entry.Number))
            )
            .ToList();
        foreach (var other in others)
        {
            var rest = other.Group.Entries.Where(entry => !recorded.Contains(entry.Number)).ToList();
            if (rest.Count == 0)
            {
                _entries.Remove(other);
                await Forget(other.Group.Id);
                continue;
            }

            var reduced = new SnapshotGroup(rest, other.Group.Type, other.Group.Id);
            _entries[_entries.IndexOf(other)] = new Entry(eventId, reduced, true);
            await Keep(eventId, reduced);
        }
    }

    /// <summary>
    /// Reads the groups the device kept for the person and the Event, and returns those that were not here yet, in the order they were
    /// made. The waiting groups of other Events stay where they are. A store that cannot be read keeps nothing.
    /// </summary>
    public async Task<IReadOnlyList<SnapshotGroup>> Restore(Guid eventId)
    {
        if (_account.Current is not { } person)
        {
            _entries.Clear();
            return [];
        }

        _entries.RemoveAll(x => x.EventId != eventId);
        var restored = new List<SnapshotGroup>();
        foreach (var group in await ReadPending(person.Id, eventId))
        {
            if (_entries.All(x => x.Group.Id != group.Id))
            {
                _entries.Add(new Entry(eventId, group, true));
                restored.Add(group);
            }
        }

        return restored;
    }

    /// <summary>Forgets every group in memory, for the page that left the Event. The kept copies stay, to be restored later.</summary>
    public void Clear()
    {
        _entries.Clear();
    }

    /// <summary>Asks the server for the group, with the group backed up in the durable store first.</summary>
    async Task<(IReadOnlyList<SnapshotReceipt> Receipts, bool Final)> PublishKept(Guid eventId, SnapshotGroup group)
    {
        if (_account.Current is not { } person)
        {
            var unkept = await _publisher.PublishSnapshotsAsync(eventId, group);
            return (unkept, SnapshotGroupRules.IsFinal(unkept.Select(SnapshotReceipts.Answer)));
        }

        var receipts = (IReadOnlyList<SnapshotReceipt>)[];
        async Task<bool> Ask(DurableSnapshotGroup _)
        {
            receipts = await _publisher.PublishSnapshotsAsync(eventId, group);
            return SnapshotGroupRules.IsFinal(receipts.Select(SnapshotReceipts.Answer));
        }

        var final = await _groups.StoreDurably(group.Id, DurableSnapshotGroup.Of(group, person.Id, eventId), Ask);
        return (receipts, final);
    }

    void Answered(Guid eventId, SnapshotGroup group, IReadOnlyList<SnapshotReceipt> receipts, bool final)
    {
        if (receipts.Any(x => x.IsRecorded))
        {
            _entries.RemoveAll(x => x.Group.Id == group.Id);
            return;
        }

        Remember(eventId, group, canBeSentAgain: !final);
    }

    /// <summary>
    /// Keeps the group in memory as waiting, or as refused. Of the refused groups of the Event only the last is kept, for a repeat of the
    /// same Snapshots.
    /// </summary>
    void Remember(Guid eventId, SnapshotGroup group, bool canBeSentAgain)
    {
        _entries.RemoveAll(x =>
            x.Group.Id == group.Id || (!canBeSentAgain && !x.CanBeSentAgain && x.EventId == eventId)
        );
        _entries.Add(new Entry(eventId, group, canBeSentAgain));
    }

    async Task<IReadOnlyList<SnapshotGroup>> ReadPending(Guid accountId, Guid eventId)
    {
        try
        {
            var kept = await _groups.ReadPendingDurably(x => x.AccountId == accountId && x.EventId == eventId);
            return [.. kept.Select(x => x.ToGroup()).OfType<SnapshotGroup>()];
        }
        catch (Exception)
        {
            return [];
        }
    }

    async Task Keep(Guid eventId, SnapshotGroup group)
    {
        try
        {
            if (_account.Current is { } person)
            {
                await _groups.KeepDurably(group.Id, DurableSnapshotGroup.Of(group, person.Id, eventId));
            }
        }
        catch (Exception)
        {
            // The kept copy stays until the group is sent again, which keeps it again.
        }
    }

    async Task Forget(Guid id)
    {
        try
        {
            await _groups.ForgetDurably(id);
        }
        catch (Exception)
        {
            // The kept copy stays until the group is sent again, which keeps it again.
        }
    }

    sealed class Entry
    {
        public Entry(Guid eventId, SnapshotGroup group, bool canBeSentAgain)
        {
            EventId = eventId;
            Group = group;
            CanBeSentAgain = canBeSentAgain;
        }

        public Guid EventId { get; }
        public SnapshotGroup Group { get; }
        public bool CanBeSentAgain { get; }
    }
}
