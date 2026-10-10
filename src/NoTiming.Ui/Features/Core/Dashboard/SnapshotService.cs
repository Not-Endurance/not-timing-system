using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Injection;
using Not.Notify;
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
/// The snapshot page's service: the Participations that are still to be timed are a view over the store (ADR-0006),
/// and the person's selection, the timestamps they capture and the history of what they sent are its own. It keeps the
/// numbers of the selected Participations, never the Participations. A group that was sent and not answered is the
/// device's until the server answers (#645, ADR-0013): it is kept before it is sent, with its id, and sent again as the same
/// group after longer and longer, whatever happens to the page, so that a lost answer never records a time twice.
/// </summary>
public class SnapshotService
    : NStatefulService,
        ISnapshotService,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    static readonly TimeSpan FIRST_RESEND = TimeSpan.FromSeconds(5);
    static readonly TimeSpan LONGEST_WAIT = TimeSpan.FromSeconds(60);

    readonly IAccountSession _account;
    readonly List<SnapshotGroup> _history = [];
    readonly INotifier _notifier;
    readonly INtsSocketContext _socketContext;
    readonly IParticipationStore _store;
    readonly ISnapshotPublisher _snapshotPublisher;
    readonly ISnapshotResendTimer _timer;
    readonly IUnansweredSnapshots _unansweredSnapshots;
    readonly object _snapshotSelectionPersistenceLock = new();
    readonly List<Snapshot> _snapshots = [];
    readonly SemaphoreSlim _sending = new(1, 1);
    readonly CancellationTokenSource _stopped = new();
    readonly IWitnessUserSession _userSessionService;
    IReadOnlyList<Participation> _participations = [];
    IReadOnlyList<Participation> _participationsToSnapshot = [];
    Task _resending = Task.CompletedTask;
    Task _snapshotSelectionPersistence = Task.CompletedTask;
    WaitingGroup? _unanswered;

    public SnapshotService(
        INtsSocketContext socketContext,
        IParticipationStore store,
        IWitnessUserSession userSessionService,
        ISnapshotPublisher snapshotPublisher,
        IAccountSession account,
        IUnansweredSnapshots unansweredSnapshots,
        ISnapshotResendTimer timer,
        INotifier notifier
    )
    {
        _socketContext = socketContext;
        _store = store;
        _userSessionService = userSessionService;
        _snapshotPublisher = snapshotPublisher;
        _account = account;
        _unansweredSnapshots = unansweredSnapshots;
        _timer = timer;
        _notifier = notifier;
        Observe(store, Rebuild);
    }

    public IReadOnlyList<Participation> Participations => _participations;
    public IReadOnlyList<Participation> ParticipationsToSnapshot => _participationsToSnapshot;
    public IReadOnlyList<Snapshot> Snapshots => _snapshots;
    public IReadOnlyList<SnapshotGroup> History => _history;

    public SnapshotGroup? Unanswered => _unanswered is { CanBeSentAgain: true } waiting ? waiting.Group : null;

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
        return await Sending(
            view,
            async () =>
            {
                var readySnapshots = _snapshots.Where(x => x.Timestamp != null).Select(CopySnapshot).ToList();
                if (readySnapshots.Count == 0)
                {
                    return false;
                }

                var (snapshotGroup, receipts) = await SendAsync(view.Event.Id, readySnapshots, snapshotType);
                return await Settle(snapshotGroup, receipts, snapshotType);
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
                var (_, receipts) = await SendAsync(
                    view.Event.Id,
                    [.. snapshotGroup.Entries.Select(CopySnapshot)],
                    snapshotType
                );
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
    /// Whether the server not recording a Snapshot is only for now: it could not be reached or answered badly (5xx, or no
    /// status), asked for time (408, 429) or said to send it again (a Participation changed by too many at once). Anything
    /// else, such as a person who may not or an Event that has ended, is its answer, and the same request gets the same one.
    /// </summary>
    static bool IsOnlyForNow(SnapshotReceipt notRecorded)
    {
        return notRecorded.Status is 0 or 408 or 429 or >= 500
            || string.Equals(notRecorded.ErrorCode, "participation-busy", StringComparison.Ordinal);
    }

    /// <summary>
    /// Sends one request of a person, one at a time: what is sent by hand and what is sent again by the app wait for each
    /// other, so that the second finds the first answered. A group that no answer came to waits, and is sent again from here.
    /// </summary>
    async Task<bool> Sending(IViewedEvent view, Func<Task<bool>> send)
    {
        await _sending.WaitAsync();
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

            _sending.Release();
        }
    }

    /// <summary>
    /// What the server recorded is no longer the person's to keep, whatever its outcome: a rejected Snapshot is an event of
    /// its own. What it did not record stays selected, to be sent again, and the person is told why.
    /// </summary>
    async Task<bool> Settle(
        SnapshotGroup snapshotGroup,
        IReadOnlyList<SnapshotReceipt> receipts,
        SnapshotType snapshotType
    )
    {
        var recorded = snapshotGroup.Entries.Where(x => IsRecorded(receipts, snapshotGroup.IdOf(x))).ToList();
        if (recorded.Count > 0)
        {
            var sent = new SnapshotGroup(recorded, snapshotType, snapshotGroup.Id);
            await DrainSnapshotSelectionPersistence();
            await KeepInHistory(sent);

            _history.Add(sent);
            FlushSnapshots(recorded.Select(x => x.Number).ToHashSet());
            Rebuild();
        }

        await AskAgainWhenRefused(receipts);
        RefuseWhenNotRecorded(receipts);
        return recorded.Count > 0;
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
    /// Sends the Snapshots as a group and says what came of it. The group is kept on the device before it goes, so that it is
    /// sent again from there if the page is lost meanwhile. A group that was sent and is not known to have been recorded,
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
            _unanswered is { } before
            && before.EventId == eventId
            && before.Group.Type == snapshotType
            && IsSame(before.Group.Entries, snapshots)
                ? before.Group
                : new SnapshotGroup(snapshots, snapshotType);
        await Keep(eventId, group);
        try
        {
            var receipts = await _snapshotPublisher.PublishSnapshotsAsync(eventId, group);
            await Answered(eventId, group, receipts);
            return (group, receipts);
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            _unanswered = new WaitingGroup(eventId, group, true);
            throw;
        }
    }

    /// <summary>
    /// The group that no answer has come to waits, and is sent again when what came was only for now; the server recording
    /// any of it, or refusing it, is its answer, and the group is the device's no more.
    /// </summary>
    async Task Answered(Guid eventId, SnapshotGroup group, IReadOnlyList<SnapshotReceipt> receipts)
    {
        if (receipts.Any(x => x.IsRecorded))
        {
            _unanswered = null;
            await Forget(eventId);
            return;
        }

        var onlyForNow = receipts.Any(IsOnlyForNow);
        _unanswered = new WaitingGroup(eventId, group, onlyForNow);
        if (!onlyForNow)
        {
            await Forget(eventId);
        }
    }

    /// <summary>
    /// Whether a group that can be sent again waits for the Event the view shows and the person may send it: a group is only
    /// sent on the authority of the Event it is of, and not while the person may not.
    /// </summary>
    bool CanResend(IViewedEvent view)
    {
        return _unanswered is { CanBeSentAgain: true } waiting && waiting.EventId == view.Event.Id && view.CanWrite;
    }

    void StartResending(IViewedEvent view)
    {
        if (_resending.IsCompleted)
        {
            _resending = ResendUntilAnswered(view);
        }
    }

    /// <summary>
    /// Sends the group that waits again after a while, and after longer and longer (up to a minute), for as long as it can be
    /// sent and the person may send it. A group the server then refuses is said to the person once and is not sent again.
    /// </summary>
    async Task ResendUntilAnswered(IViewedEvent view)
    {
        var wait = FIRST_RESEND;
        while (!_stopped.IsCancellationRequested && CanResend(view))
        {
            try
            {
                await _timer.Wait(wait, _stopped.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            wait = TimeSpan.FromTicks(Math.Min(wait.Ticks * 2, LONGEST_WAIT.Ticks));
            try
            {
                await ResendAsync(view);
            }
            catch (DomainException ex)
            {
                _notifier.Warn(ex.Message);
            }
            catch (OperationCanceledException) when (_stopped.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // The server was not reached again: the next round.
            }
        }
    }

    async Task ResendAsync(IViewedEvent view)
    {
        await _sending.WaitAsync(_stopped.Token);
        try
        {
            // What was true when the wait began may not be now: the group is answered, or the person may no longer send it.
            if (!CanResend(view))
            {
                return;
            }

            var waiting = _unanswered!;
            var (group, receipts) = await SendAsync(
                waiting.EventId,
                [.. waiting.Group.Entries.Select(CopySnapshot)],
                waiting.Group.Type
            );
            await Settle(group, receipts, group.Type);
        }
        finally
        {
            _sending.Release();
        }
    }

    /// <summary>
    /// The group the device kept for the person and the Event, if there is one, selected again with the times it was made
    /// with. What the page holds of the same Event stays when the device holds nothing, as a storage that cannot be written
    /// keeps nothing and the group is then sent from the page for as long as the page is open.
    /// </summary>
    async Task RestoreUnanswered()
    {
        await _account.Load();
        if (_account.Current is not { } person || _socketContext.Event is not { } selected)
        {
            _unanswered = null;
            return;
        }

        if (_unanswered is { } held && held.EventId != selected.Id)
        {
            _unanswered = null;
        }

        if (await _unansweredSnapshots.Read(person.Id, selected.Id) is not { } kept)
        {
            return;
        }

        _unanswered = new WaitingGroup(selected.Id, kept, true);
        foreach (var entry in kept.Entries)
        {
            // What is sent is what was kept: the time it was captured at takes the place of what the session had of it.
            _snapshots.RemoveAll(x => x.Number == entry.Number);
            _snapshots.Add(CopySnapshot(entry));
        }
    }

    async Task Keep(Guid eventId, SnapshotGroup group)
    {
        if (_account.Current is { } person)
        {
            await _unansweredSnapshots.Keep(person.Id, eventId, group);
        }
    }

    async Task Forget(Guid eventId)
    {
        if (_account.Current is { } person)
        {
            await _unansweredSnapshots.Forget(person.Id, eventId);
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

    /// <summary>The group that was sent and not answered, and whether the server may answer it yet: only when what came was for now.</summary>
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
