using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Injection;
using NTS.Application.UserSession;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Snapshots;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NoTiming.Ui.Features.Core.Dashboard;

/// <summary>
/// The snapshot page's service: the Participations that are still to be timed are a view over the store (ADR-0006),
/// and the person's selection, the timestamps they capture and the history of what they sent are its own. It keeps the
/// numbers of the selected Participations, never the Participations.
/// </summary>
public class SnapshotService
    : NStatefulService,
        ISnapshotService,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly List<SnapshotGroup> _history = [];
    readonly INtsSocketContext _socketContext;
    readonly IParticipationStore _store;
    readonly ISnapshotPublisher _snapshotPublisher;
    readonly object _snapshotSelectionPersistenceLock = new();
    readonly List<Snapshot> _snapshots = [];
    readonly IWitnessUserSession _userSessionService;
    IReadOnlyList<Participation> _participations = [];
    IReadOnlyList<Participation> _participationsToSnapshot = [];
    Task _snapshotSelectionPersistence = Task.CompletedTask;
    SnapshotGroup? _unanswered;

    public SnapshotService(
        INtsSocketContext socketContext,
        IParticipationStore store,
        IWitnessUserSession userSessionService,
        ISnapshotPublisher snapshotPublisher
    )
    {
        _socketContext = socketContext;
        _store = store;
        _userSessionService = userSessionService;
        _snapshotPublisher = snapshotPublisher;
        Observe(store, Rebuild);
    }

    public IReadOnlyList<Participation> Participations => _participations;
    public IReadOnlyList<Participation> ParticipationsToSnapshot => _participationsToSnapshot;
    public IReadOnlyList<Snapshot> Snapshots => _snapshots;
    public IReadOnlyList<SnapshotGroup> History => _history;

    protected override async Task<bool> InitializeState()
    {
        if (_socketContext.Event == null)
        {
            return false;
        }

        await _store.Load();
        var session = await _userSessionService.GetCurrent();

        _snapshots.Clear();
        _history.Clear();
        _history.AddRange(session?.GetSnapshotHistory() ?? []);
        RestoreSnapshotSelections(session?.GetSnapshotSelections() ?? []);
        BuildViews();

        return _participations.Any() || _participationsToSnapshot.Any() || _history.Any();
    }

    public void Capture(Snapshot snapshot)
    {
        GuardHelper.ThrowIfDefault(snapshot);
        UpdateTimestamp(snapshot, new Timestamp(DateTimeOffset.Now));
    }

    public void SelectForSnapshot(Participation participation)
    {
        GuardHelper.ThrowIfDefault(participation);

        if (_snapshots.Any(x => x.Number == participation.Combination.Number))
        {
            return;
        }

        _snapshots.Add(
            new Snapshot(
                participation.Combination.Number,
                participation.Combination.Athlete.Name,
                participation.Combination.Athlete.NameEnglish,
                ruleset: participation.Competition.Ruleset
            )
        );
        QueueSnapshotSelectionPersistence();
        Rebuild();
    }

    public void Remove(Snapshot snapshot)
    {
        GuardHelper.ThrowIfDefault(snapshot);

        if (_snapshots.All(x => x.Number != snapshot.Number))
        {
            return;
        }

        FlushSnapshots([snapshot.Number]);
        QueueSnapshotSelectionPersistence();
        Rebuild();
    }

    public async Task<bool> Publish(IViewedEvent view, SnapshotType snapshotType)
    {
        view.EnsureCanWrite();
        var readySnapshots = _snapshots.Where(x => x.Timestamp != null).Select(CopySnapshot).ToList();
        if (readySnapshots.Count == 0)
        {
            return false;
        }

        var (snapshotGroup, receipts) = await SendAsync(view.Event.Id, readySnapshots, snapshotType);

        // What the server recorded is no longer the person's to keep, whatever its outcome: a rejected Snapshot is an event
        // of its own. What it did not record stays selected, to be sent again, and the person is told why.
        var recorded = snapshotGroup.Entries.Where(x => IsRecorded(receipts, snapshotGroup.IdOf(x))).ToList();
        if (recorded.Count > 0)
        {
            var sent = new SnapshotGroup(recorded, snapshotType, snapshotGroup.Id);
            await DrainSnapshotSelectionPersistence();
            await _userSessionService.AppendSnapshot(sent);

            _history.Add(sent);
            FlushSnapshots(recorded.Select(x => x.Number).ToHashSet());
            Rebuild();
        }

        RefuseWhenNotRecorded(receipts);
        return recorded.Count > 0;
    }

    public async Task RePublish(IViewedEvent view, SnapshotGroup snapshotGroup, SnapshotType snapshotType)
    {
        view.EnsureCanWrite();
        GuardHelper.ThrowIfDefault(snapshotGroup);
        var (_, receipts) = await SendAsync(
            view.Event.Id,
            [.. snapshotGroup.Entries.Select(CopySnapshot)],
            snapshotType
        );
        RefuseWhenNotRecorded(receipts);
    }

    public void UpdateTimestamp(Snapshot snapshot, Timestamp timestamp)
    {
        GuardHelper.ThrowIfDefault(snapshot);
        GuardHelper.ThrowIfDefault(timestamp);

        var existingSnapshot = _snapshots.FirstOrDefault(x => x.Number == snapshot.Number);
        if (existingSnapshot == null)
        {
            return;
        }

        existingSnapshot.Timestamp = timestamp;
        QueueSnapshotSelectionPersistence();
        EmitChanged();
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        _unanswered = null;
        _history.Clear();
        _snapshots.Clear();
        _participations = [];
        _participationsToSnapshot = [];
        ClearState();
        return Task.CompletedTask;
    }

    static bool IsRecorded(IReadOnlyList<SnapshotReceipt> receipts, Guid id)
    {
        return receipts.Any(x => x.Id == id && x.IsRecorded);
    }

    static bool IsSame(IEnumerable<Snapshot> sent, IReadOnlyList<Snapshot> snapshots)
    {
        var before = sent.ToList();
        return before.Count == snapshots.Count
            && before.Zip(snapshots, (x, y) => x.Number == y.Number && x.Timestamp == y.Timestamp).All(same => same);
    }

    /// <summary>The person is told why the first Snapshot that was not recorded was not: the Api's own words.</summary>
    static void RefuseWhenNotRecorded(IReadOnlyList<SnapshotReceipt> receipts)
    {
        if (receipts.FirstOrDefault(x => !x.IsRecorded) is { } failed)
        {
            throw new DomainException(failed.ErrorMessage ?? failed.ErrorCode ?? "The Snapshot was not recorded.");
        }
    }

    /// <summary>
    /// Sends the Snapshots as a group and says what came of it. A group that was sent and is not known to have been recorded,
    /// because the request failed or the server recorded none of it, is sent again as the same group, with the same ids, when
    /// the same Snapshots are sent as the same kind: what the server did record, and whose answer was lost, comes back with
    /// its first outcome and is not recorded twice (ADR-0013). Anything else is a new gesture, with new ids.
    /// </summary>
    async Task<(SnapshotGroup Group, IReadOnlyList<SnapshotReceipt> Receipts)> SendAsync(
        Guid eventId,
        IReadOnlyList<Snapshot> snapshots,
        SnapshotType snapshotType
    )
    {
        var group =
            _unanswered is { } before && before.Type == snapshotType && IsSame(before.Entries, snapshots)
                ? before
                : new SnapshotGroup(snapshots, snapshotType);
        try
        {
            var receipts = await _snapshotPublisher.PublishSnapshotsAsync(eventId, group);
            _unanswered = receipts.Any(x => x.IsRecorded) ? null : group;
            return (group, receipts);
        }
        catch
        {
            _unanswered = group;
            throw;
        }
    }

    void FlushSnapshots(HashSet<int> participationNumbers)
    {
        _snapshots.RemoveAll(x => participationNumbers.Contains(x.Number));
    }

    void Rebuild()
    {
        BuildViews();
        EmitChanged();
    }

    /// <summary>The selectable list and the selected Participations, from the store as it is now.</summary>
    void BuildViews()
    {
        var selected = _snapshots.Select(x => x.Number).ToHashSet();
        _participations =
        [
            .. _store.Participations.Where(x =>
                !x.IsComplete() && !x.IsEliminated() && !selected.Contains(x.Combination.Number)
            ),
        ];
        _participationsToSnapshot =
        [
            .. _snapshots
                .Select(x => _store.Participations.FirstOrDefault(y => y.Combination.Number == x.Number))
                .OfType<Participation>(),
        ];
    }

    void RestoreSnapshotSelections(IReadOnlyList<Snapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var stillToBeTimed = _store.Participations.Any(x =>
                x.Combination.Number == snapshot.Number && !x.IsComplete() && !x.IsEliminated()
            );
            if (!stillToBeTimed || _snapshots.Any(x => x.Number == snapshot.Number))
            {
                continue;
            }

            _snapshots.Add(CopySnapshot(snapshot));
        }
    }

    void QueueSnapshotSelectionPersistence()
    {
        var snapshots = _snapshots.Select(CopySnapshot).ToArray();

        lock (_snapshotSelectionPersistenceLock)
        {
            _snapshotSelectionPersistence = PersistSnapshotSelectionsAfter(_snapshotSelectionPersistence, snapshots);
        }
    }

    async Task PersistSnapshotSelectionsAfter(Task previous, IReadOnlyCollection<Snapshot> snapshots)
    {
        await Task.Yield();

        try
        {
            await previous;
        }
        catch
        {
            // Previous selection persistence is intentionally best effort.
        }

        try
        {
            await _userSessionService.ReplaceSnapshotSelections(snapshots);
        }
        catch
        {
            // Snapshot selection persistence must not block witness timing flow.
        }
    }

    async Task DrainSnapshotSelectionPersistence()
    {
        while (true)
        {
            var persistence = GetSnapshotSelectionPersistence();

            try
            {
                await persistence;
            }
            catch
            {
                // Background persistence failures are intentionally ignored.
            }

            if (ReferenceEquals(persistence, GetSnapshotSelectionPersistence()))
            {
                return;
            }
        }
    }

    Task GetSnapshotSelectionPersistence()
    {
        lock (_snapshotSelectionPersistenceLock)
        {
            return _snapshotSelectionPersistence;
        }
    }

    static Snapshot CopySnapshot(Snapshot snapshot)
    {
        return new Snapshot(
            snapshot.Number,
            snapshot.Name,
            snapshot.NameEnglish,
            snapshot.Timestamp == null ? null : Timestamp.Copy(snapshot.Timestamp),
            snapshot.Ruleset
        );
    }
}
