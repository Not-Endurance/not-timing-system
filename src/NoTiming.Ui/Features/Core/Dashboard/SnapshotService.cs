using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Injection;
using Not.Notify;
using NoTiming.Ui.Storage.Browser;
using NTS.Application.UserSession;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Snapshots;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NoTiming.Ui.Features.Core.Dashboard;

/// <summary>
/// The snapshot page's service: the Participations that are still to be timed are a view over the store (ADR-0006), and the
/// person's selection, the timestamps they capture and the history of what they sent are its own. Every group that was sent and
/// is not answered for good is kept in the durable store under its own id (#645, ADR-0013), and is sent again as the same group,
/// with the same ids, so that a lost answer never records a time twice and several groups wait together while the API is down.
/// </summary>
public class SnapshotService
    : NStatefulService,
        ISnapshotService,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped,
        IRetryable
{
    const string SELECTIONS_SAVE = "selections";
    static readonly TimeSpan FIRST_RESEND = TimeSpan.FromSeconds(5);
    static readonly TimeSpan LONGEST_WAIT = TimeSpan.FromSeconds(60);

    readonly IAccountSession _account;
    readonly IDelay _delay;
    readonly IDurableRepository<DurableSnapshotGroup> _groups;
    readonly List<SnapshotGroup> _history = [];
    readonly INotifier _notifier;
    readonly INtsSocketContext _socketContext;
    readonly IParticipationStore _store;
    readonly ISnapshotPublisher _snapshotPublisher;
    readonly CoalesceInvoker<string> _savingSelections = new();
    readonly List<Snapshot> _snapshots = [];
    readonly List<WaitingGroup> _unanswered = [];
    readonly Gate _sending = new();
    readonly CancellationTokenSource _stopped = new();
    readonly IWitnessUserSession _userSessionService;
    IReadOnlyList<Participation> _participations = [];
    IReadOnlyList<Participation> _participationsToSnapshot = [];
    Task _resending = Task.CompletedTask;
    Task _selectionsSaved = Task.CompletedTask;
    IViewedEvent? _resendingFor;

    public SnapshotService(
        INtsSocketContext socketContext,
        IParticipationStore store,
        IWitnessUserSession userSessionService,
        ISnapshotPublisher snapshotPublisher,
        IAccountSession account,
        IDurableRepository<DurableSnapshotGroup> groups,
        IDelay delay,
        INotifier notifier
    )
    {
        _socketContext = socketContext;
        _store = store;
        _userSessionService = userSessionService;
        _snapshotPublisher = snapshotPublisher;
        _account = account;
        _groups = groups;
        _delay = delay;
        _notifier = notifier;
        Observe(store, Rebuild);
    }

    bool IRetryable.CanRetry => _resendingFor is { } view && CanResend(view);

    public IReadOnlyList<Participation> Participations => _participations;
    public IReadOnlyList<Participation> ParticipationsToSnapshot => _participationsToSnapshot;
    public IReadOnlyList<Snapshot> Snapshots => _snapshots;
    public IReadOnlyList<SnapshotGroup> History => _history;

    /// <summary>The first group that waits to be sent again; none when nothing waits.</summary>
    public SnapshotGroup? Unanswered => Waiting.FirstOrDefault();

    /// <summary>Every group that waits to be sent again, in the order they were made.</summary>
    public IReadOnlyList<SnapshotGroup> Waiting => [.. _unanswered.Where(x => x.CanBeSentAgain).Select(x => x.Group)];

    /// <summary>Completes when no group is being sent again.</summary>
    public Task Resending => _resending;

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
        await RestoreUnanswered();
        BuildViews();

        return _participations.Any() || _participationsToSnapshot.Any() || _history.Any();
    }

    public override void Dispose()
    {
        _stopped.Cancel();
        _resendingFor = null;
        base.Dispose();
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
        SaveSelections();
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
        SaveSelections();
        Rebuild();
    }

    public async Task<bool> Publish(IViewedEvent view, SnapshotType snapshotType)
    {
        view.EnsureCanWrite();
        return await Sending(
            view,
            async () =>
            {
                var readySnapshots = _snapshots.Where(x => x.Timestamp != null).Select(CopySnapshot).ToList();
                if (readySnapshots.Count == 0)
                {
                    return false;
                }

                var group = GroupFor(view.Event.Id, readySnapshots, snapshotType);
                var receipts = await Send(view.Event.Id, group);
                return await Settle(view.Event.Id, group, receipts, snapshotType);
            }
        );
    }

    public async Task RePublish(IViewedEvent view, SnapshotGroup snapshotGroup, SnapshotType snapshotType)
    {
        view.EnsureCanWrite();
        GuardHelper.ThrowIfDefault(snapshotGroup);
        await Sending(
            view,
            async () =>
            {
                var snapshots = snapshotGroup.Entries.Select(CopySnapshot).ToList();
                if (snapshots.Count == 0)
                {
                    return true;
                }

                var group = GroupFor(view.Event.Id, snapshots, snapshotType);
                var receipts = await Send(view.Event.Id, group);
                await ReleaseRecorded(view.Event.Id, group, receipts);
                await AskAgainWhenRefused(receipts);
                RefuseWhenNotRecorded(receipts);
                return true;
            }
        );
    }

    public void Resume(IViewedEvent view)
    {
        if (CanResend(view))
        {
            StartResending(view);
        }
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
        SaveSelections();
        EmitChanged();
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        _unanswered.Clear();
        _resendingFor = null;
        _history.Clear();
        _snapshots.Clear();
        _participations = [];
        _participationsToSnapshot = [];
        ClearState();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends every group of the Event that waits again, in the order they were made. A group the server then refuses is said to the
    /// person once and is not sent again. A failure to reach the server is not a stop: the next round tries again.
    /// </summary>
    async Task<bool> IRetryable.RetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _sending.WaitToEnter(cancellationToken);
            try
            {
                // What was true when the wait began may not be now: a group is answered, or the person may no longer send it.
                if (_resendingFor is { } view)
                {
                    // Each group is read as it is when its turn comes: a group that an earlier send changed or answered is sent as that.
                    var ids = _unanswered
                        .Where(x => x.CanBeSentAgain && x.EventId == view.Event.Id)
                        .Select(x => x.Group.Id)
                        .ToList();
                    foreach (var id in ids)
                    {
                        if (!view.CanWrite)
                        {
                            break;
                        }

                        if (_unanswered.FirstOrDefault(x => x.Group.Id == id && x.CanBeSentAgain) is { } current)
                        {
                            await ResendOne(current, cancellationToken);
                        }
                    }
                }
            }
            finally
            {
                _sending.Exit();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the page has ended, and the loop ends with it
        }

        return !((IRetryable)this).CanRetry;
    }

    /// <summary>
    /// Sends one waiting group again. A failure that is not the server's answer, such as a request that timed out, leaves the group
    /// waiting for the next round.
    /// </summary>
    async Task ResendOne(WaitingGroup waiting, CancellationToken cancellationToken)
    {
        try
        {
            var receipts = await Send(waiting.EventId, waiting.Group);
            await Settle(waiting.EventId, waiting.Group, receipts, waiting.Group.Type);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DomainException ex)
        {
            _notifier.Warn(ex.Message);
        }
        catch (Exception)
        {
            // The server was not reached again: the next round.
        }
    }

    /// <summary>
    /// Sends one request of a person, one at a time: what is sent by hand and what is sent again by the app wait for each
    /// other, so that the second finds the first answered. A group that no answer came to waits, and is sent again from here.
    /// </summary>
    async Task<bool> Sending(IViewedEvent view, Func<Task<bool>> send)
    {
        await _sending.WaitToEnter();
        try
        {
            return await send();
        }
        finally
        {
            if (CanResend(view))
            {
                StartResending(view);
            }

            _sending.Exit();
        }
    }

    /// <summary>
    /// What the server recorded is no longer the person's to keep, whatever its outcome: a rejected Snapshot is an event of
    /// its own. What it did not record stays selected, to be sent again, and the person is told why.
    /// </summary>
    async Task<bool> Settle(
        Guid eventId,
        SnapshotGroup snapshotGroup,
        IReadOnlyList<SnapshotReceipt> receipts,
        SnapshotType snapshotType
    )
    {
        var recorded = snapshotGroup.Entries.Where(x => IsRecorded(receipts, snapshotGroup.IdOf(x))).ToList();
        if (recorded.Count > 0)
        {
            var sent = new SnapshotGroup(recorded, snapshotType, snapshotGroup.Id);
            await _selectionsSaved;
            await KeepInHistory(sent);

            _history.Add(sent);
            FlushSnapshots(recorded.Select(x => x.Number).ToHashSet());
            await ReleaseRecorded(eventId, snapshotGroup, receipts);
            Rebuild();
        }

        await AskAgainWhenRefused(receipts);
        RefuseWhenNotRecorded(receipts);
        return recorded.Count > 0;
    }

    /// <summary>
    /// The Snapshots that the server recorded are answered. A group that waits for the Event and holds one of them no longer sends it,
    /// so that it is not recorded twice; a group left with nothing is forgotten. A failure to change the kept copy is not a failure of
    /// the send: the next send of the group keeps it again.
    /// </summary>
    async Task ReleaseRecorded(Guid eventId, SnapshotGroup group, IReadOnlyList<SnapshotReceipt> receipts)
    {
        var recorded = group.Entries.Where(x => IsRecorded(receipts, group.IdOf(x))).Select(x => x.Number).ToHashSet();
        if (recorded.Count == 0)
        {
            return;
        }

        var waiting = _unanswered
            .Where(x =>
                x.CanBeSentAgain
                && x.EventId == eventId
                && x.Group.Id != group.Id
                && x.Group.Entries.Any(entry => recorded.Contains(entry.Number))
            )
            .ToList();
        foreach (var other in waiting)
        {
            var rest = other.Group.Entries.Where(entry => !recorded.Contains(entry.Number)).ToList();
            if (rest.Count == 0)
            {
                _unanswered.Remove(other);
                await ForgetKept(other.Group.Id);
                continue;
            }

            var reduced = new SnapshotGroup(rest, other.Group.Type, other.Group.Id);
            _unanswered[_unanswered.IndexOf(other)] = new WaitingGroup(eventId, reduced, true);
            await KeepKept(eventId, reduced);
        }
    }

    async Task ForgetKept(Guid id)
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

    async Task KeepKept(Guid eventId, SnapshotGroup group)
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

    /// <summary>
    /// A refusal that says the person may not (not signed in, not allowed) says that what the app shows of them is out of date:
    /// access that was removed shows on the next request, as the account is asked again and the controls follow it.
    /// </summary>
    async Task AskAgainWhenRefused(IReadOnlyList<SnapshotReceipt> receipts)
    {
        if (receipts.Any(x => !x.IsRecorded && x.Status is 401 or 403))
        {
            await _account.Refresh();
        }
    }

    /// <summary>
    /// The group a repeat of the same Snapshots as the same kind is sent as, waiting or refused (ADR-0013): otherwise a new group, with
    /// new ids.
    /// </summary>
    SnapshotGroup GroupFor(Guid eventId, IReadOnlyList<Snapshot> snapshots, SnapshotType snapshotType)
    {
        var refused = _unanswered.FirstOrDefault(x =>
            x.EventId == eventId && SnapshotGroupRules.IsSentAgain(x.Group, snapshotType, snapshots)
        );
        return refused?.Group ?? new SnapshotGroup(snapshots, snapshotType);
    }

    /// <summary>
    /// Asks the server for the group. The group is kept on the device until the server answers for good, under its own id, so that
    /// several groups wait together. A group that no answer came to, or whose answer is only for now, keeps waiting; a recorded one
    /// is no longer kept; a refused one is kept only for a repeat of the same Snapshots.
    /// </summary>
    async Task<IReadOnlyList<SnapshotReceipt>> Send(Guid eventId, SnapshotGroup group)
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
    /// Asks the server for the group, with the group backed up in the durable store first. When the backup cannot be written the
    /// durable store does not ask the server at all, and the group is sent from the page alone.
    /// </summary>
    async Task<(IReadOnlyList<SnapshotReceipt> Receipts, bool Final)> PublishKept(Guid eventId, SnapshotGroup group)
    {
        if (_account.Current is not { } person)
        {
            var unkept = await _snapshotPublisher.PublishSnapshotsAsync(eventId, group);
            return (unkept, SnapshotGroupRules.IsFinal(unkept.Select(Answer)));
        }

        var receipts = (IReadOnlyList<SnapshotReceipt>)[];
        var asked = false;
        async Task<bool> Ask(DurableSnapshotGroup _)
        {
            asked = true;
            receipts = await _snapshotPublisher.PublishSnapshotsAsync(eventId, group);
            return SnapshotGroupRules.IsFinal(receipts.Select(Answer));
        }

        try
        {
            var final = await _groups.StoreDurably(group.Id, DurableSnapshotGroup.Of(group, person.Id, eventId), Ask);
            return (receipts, final);
        }
        catch (Exception) when (!asked)
        {
            receipts = await _snapshotPublisher.PublishSnapshotsAsync(eventId, group);
            return (receipts, SnapshotGroupRules.IsFinal(receipts.Select(Answer)));
        }
    }

    /// <summary>A group the server recorded is no longer kept. Any other group keeps waiting, unless its answer is final.</summary>
    void Answered(Guid eventId, SnapshotGroup group, IReadOnlyList<SnapshotReceipt> receipts, bool final)
    {
        if (receipts.Any(x => x.IsRecorded))
        {
            _unanswered.RemoveAll(x => x.Group.Id == group.Id);
            return;
        }

        Remember(eventId, group, canBeSentAgain: !final);
    }

    /// <summary>
    /// Keeps the group in memory as waiting, or as refused. Of the refused groups of the Event only the last is kept, for a repeat of
    /// the same Snapshots.
    /// </summary>
    void Remember(Guid eventId, SnapshotGroup group, bool canBeSentAgain)
    {
        _unanswered.RemoveAll(x =>
            x.Group.Id == group.Id || (!canBeSentAgain && !x.CanBeSentAgain && x.EventId == eventId)
        );
        _unanswered.Add(new WaitingGroup(eventId, group, canBeSentAgain));
    }

    /// <summary>
    /// Whether a group that waits for the Event the view shows may be sent: a group is only sent on the authority of the Event it is of,
    /// and not while the person may not.
    /// </summary>
    bool CanResend(IViewedEvent view)
    {
        return view.CanWrite && _unanswered.Any(x => x.CanBeSentAgain && x.EventId == view.Event.Id);
    }

    /// <summary>
    /// Starts the loop that sends the waiting groups again, on the authority of the view it starts with: a loop that is running keeps
    /// the view it began with, so that the group of another Event is not sent by it.
    /// </summary>
    void StartResending(IViewedEvent view)
    {
        if (_resending.IsCompleted)
        {
            _resendingFor = view;
            _resending = RetryUntilDone.Run(_delay, this, FIRST_RESEND, LONGEST_WAIT, _stopped.Token);
        }
    }

    /// <summary>
    /// The groups the device kept for the person and the Event, selected again with the times they were made with. A group that is
    /// already here is not added twice. A store that cannot be read keeps nothing, and the page goes on with what the session holds.
    /// </summary>
    async Task RestoreUnanswered()
    {
        await _account.Load();
        if (_account.Current is not { } person || _socketContext.Event is not { } selected)
        {
            _unanswered.Clear();
            return;
        }

        _unanswered.RemoveAll(x => x.EventId != selected.Id);
        foreach (var group in await ReadPending(person.Id, selected.Id))
        {
            if (_unanswered.Any(x => x.Group.Id == group.Id))
            {
                continue;
            }

            _unanswered.Add(new WaitingGroup(selected.Id, group, true));
            foreach (var entry in group.Entries)
            {
                // What is sent is what was kept: the time it was captured at takes the place of what the session had of it.
                _snapshots.RemoveAll(x => x.Number == entry.Number);
                _snapshots.Add(CopySnapshot(entry));
            }
        }
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

    /// <summary>
    /// Saves the selections as they are now. A save that is waiting takes the newest selections, and the page waits for the last
    /// one before it writes history.
    /// </summary>
    void SaveSelections()
    {
        var snapshots = _snapshots.Select(CopySnapshot).ToArray();
        _selectionsSaved = _savingSelections.Invoke(SELECTIONS_SAVE, () => SaveSelectionsAsync(snapshots));
    }

    /// <summary>
    /// The selections are the person's session, and the server has the Snapshots themselves: a save that fails does not block the
    /// timing, and the next change saves again.
    /// </summary>
    async Task SaveSelectionsAsync(IReadOnlyCollection<Snapshot> snapshots)
    {
        try
        {
            await _userSessionService.ReplaceSnapshotSelections(snapshots);
        }
        catch
        {
            // Snapshot selection persistence must not block witness timing flow.
        }
    }

    /// <summary>
    /// The person's own record of what they sent is a convenience, and the server already has the Snapshots: a record that
    /// cannot be kept (the host failed, the session ended) does not undo what was sent, or leave it selected to be sent twice.
    /// </summary>
    async Task KeepInHistory(SnapshotGroup sent)
    {
        try
        {
            await _userSessionService.AppendSnapshot(sent);
        }
        catch
        {
            // The history of this page still shows it, as it is held in memory.
        }
    }

    /// <summary>The person is told why the first Snapshot that was not recorded was not: the Api's own words.</summary>
    static void RefuseWhenNotRecorded(IReadOnlyList<SnapshotReceipt> receipts)
    {
        if (receipts.FirstOrDefault(x => !x.IsRecorded) is { } failed)
        {
            throw new DomainException(failed.ErrorMessage ?? failed.ErrorCode ?? "The Snapshot was not recorded.");
        }
    }

    static SnapshotAnswer Answer(SnapshotReceipt receipt)
    {
        return new SnapshotAnswer(receipt.IsRecorded, receipt.Status, receipt.ErrorCode);
    }

    static bool IsRecorded(IReadOnlyList<SnapshotReceipt> receipts, Guid id)
    {
        return receipts.Any(x => x.Id == id && x.IsRecorded);
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

    /// <summary>A group that was sent and not answered for good, and whether the server may answer it yet: only when what came was for now.</summary>
    sealed class WaitingGroup
    {
        public WaitingGroup(Guid eventId, SnapshotGroup group, bool canBeSentAgain)
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
