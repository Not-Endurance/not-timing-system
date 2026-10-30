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
/// person's selection, the timestamps they capture and the history of what they sent are its own. What was sent and is not answered
/// for good waits in <see cref="WaitingGroups"/> (#645, ADR-0013), and is sent again until the server answers.
/// </summary>
public class SnapshotService
    : NStatefulService,
        ISnapshotService,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly IAccountSession _account;
    readonly IDurableRepository<DurableSnapshotGroup> _groups;
    readonly List<SnapshotGroup> _history = [];
    readonly INotifier _notifier;
    readonly INtsSocketContext _socketContext;
    readonly IParticipationStore _store;
    readonly ISnapshotPublisher _snapshotPublisher;
    readonly List<Snapshot> _snapshots = [];
    readonly WaitingGroups _waiting;
    readonly SelectionSaver _selections;
    readonly Gate _sending = new();
    readonly HashSet<SnapshotGroup> _withoutId = [];
    readonly CancellationTokenSource _stopped = new();
    readonly IWitnessUserSession _userSessionService;
    IReadOnlyList<Participation> _participations = [];
    IReadOnlyList<Participation> _participationsToSnapshot = [];
    Task _resending = Task.CompletedTask;

    public SnapshotService(
        INtsSocketContext socketContext,
        IParticipationStore store,
        IWitnessUserSession userSessionService,
        ISnapshotPublisher snapshotPublisher,
        IAccountSession account,
        IDurableRepository<DurableSnapshotGroup> groups,
        INotifier notifier
    )
    {
        _socketContext = socketContext;
        _store = store;
        _userSessionService = userSessionService;
        _account = account;
        _groups = groups;
        _notifier = notifier;
        _snapshotPublisher = snapshotPublisher;
        _waiting = new WaitingGroups(account, groups, snapshotPublisher);
        _selections = new SelectionSaver(userSessionService);
        Observe(store, Rebuild);
    }

    public IReadOnlyList<Participation> Participations => _participations;
    public IReadOnlyList<Participation> ParticipationsToSnapshot => _participationsToSnapshot;
    public IReadOnlyList<Snapshot> Snapshots => _snapshots;
    public IReadOnlyList<SnapshotGroup> History => _history;

    /// <summary>The first group that waits to be sent again; none when nothing waits.</summary>
    public SnapshotGroup? Unanswered => Waiting.FirstOrDefault();

    /// <summary>Every group that waits to be sent again, in the order they were made.</summary>
    public IReadOnlyList<SnapshotGroup> Waiting => _waiting.Waiting;

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
        _withoutId.Clear();
        foreach (var stored in session?.SnapshotHistory ?? [])
        {
            var group = stored.MapToDomain();
            _history.Add(group);
            if (stored.GroupId == null)
            {
                _withoutId.Add(group); // kept before the ids were: the server cannot be asked of its Snapshots
            }
        }

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

                var group =
                    _waiting.RepeatOf(view.Event.Id, readySnapshots, snapshotType)
                    ?? new SnapshotGroup(readySnapshots, snapshotType);
                var receipts = await _waiting.Send(view.Event.Id, group);
                return await Settle(view, group, receipts, snapshotType);
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

                var group =
                    _waiting.RepeatOf(view.Event.Id, snapshots, snapshotType)
                    ?? new SnapshotGroup(snapshots, snapshotType);
                var receipts = await _waiting.Send(view.Event.Id, group);
                await _waiting.Release(view.Event.Id, group, receipts);
                await AskAgainWhenRefused(receipts);
                SnapshotReceipts.RefuseWhenNotRecorded(view, receipts);
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

    public bool CanUpdate(SnapshotGroup group, Snapshot entry)
    {
        return entry.Timestamp != null
            && group.Type is SnapshotType.Arrive or SnapshotType.Present // the kinds that an Update names
            && !_withoutId.Contains(group)
            && ReferenceEquals(LatestOf(entry.Number, group.Type), group);
    }

    public async Task UpdateTime(IViewedEvent view, SnapshotGroup group, Snapshot entry, Timestamp time)
    {
        view.EnsureCanWrite();
        GuardHelper.ThrowIfDefault(group);
        GuardHelper.ThrowIfDefault(entry);
        GuardHelper.ThrowIfDefault(time);
        await Sending(
            view,
            async () =>
            {
                if (!CanUpdate(group, entry))
                {
                    throw new DomainException(Snapshot_not_the_latest_string);
                }

                var receipt = await _snapshotPublisher.UpdateSnapshotAsync(
                    group.IdOf(entry),
                    time.ToDateTimeOffset(),
                    _stopped.Token
                );
                await AskAgainWhenRefused([receipt]);
                SnapshotReceipts.RefuseWhenNotRecorded(view, [receipt]);
                if (!receipt.IsAccepted)
                {
                    throw new DomainException(
                        string.Format(Time_not_taken_string, OutcomeWords.Of(receipt.Snapshot!.Outcome.ToString()))
                    );
                }

                entry.Timestamp = time;
                await KeepTimeInHistory(group, entry);
                EmitChanged();
                return true;
            }
        );
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        _waiting.Clear();
        _history.Clear();
        _withoutId.Clear();
        _snapshots.Clear();
        _participations = [];
        _participationsToSnapshot = [];
        ClearState();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends every group of the Event that waits again, in the order they were made, for as long as the view that started the
    /// retries may send. A group the server then refuses is said to the person once and is not sent again. A failure to reach the
    /// server is not a stop: the next round tries again. Reports true when no group waits any longer.
    /// </summary>
    async Task<bool> ResendRound(IViewedEvent view, CancellationToken cancellationToken)
    {
        await _sending.WaitToEnter(cancellationToken);
        try
        {
            // What was true when the wait began may not be now: a group is answered, or the person may no longer send it.
            // Each group is read as it is when its turn comes: a group that an earlier send changed or answered is sent as that.
            foreach (var id in _waiting.IdsWaitingFor(view.Event.Id))
            {
                if (!view.CanWrite)
                {
                    break;
                }

                await ResendOne(view, id, cancellationToken);
            }
        }
        finally
        {
            _sending.Exit();
        }

        return !CanResend(view);
    }

    /// <summary>
    /// Sends one waiting group again. A failure that is not the server's answer, such as a request that timed out, leaves the group
    /// waiting for the next round.
    /// </summary>
    async Task ResendOne(IViewedEvent view, Guid id, CancellationToken cancellationToken)
    {
        if (_waiting.Find(id) is not { } waiting)
        {
            return;
        }

        try
        {
            var receipts = await _waiting.Send(waiting.EventId, waiting.Group);
            await Settle(view, waiting.Group, receipts, waiting.Group.Type);
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
        IViewedEvent view,
        SnapshotGroup snapshotGroup,
        IReadOnlyList<SnapshotReceipt> receipts,
        SnapshotType snapshotType
    )
    {
        var recorded = snapshotGroup
            .Entries.Where(x => SnapshotReceipts.IsRecorded(receipts, snapshotGroup.IdOf(x)))
            .ToList();
        if (recorded.Count > 0)
        {
            var sent = new SnapshotGroup(recorded, snapshotType, snapshotGroup.Id);
            await _selections.Saved;
            await KeepInHistory(sent);

            _history.Add(sent);
            FlushSnapshots(recorded.Select(x => x.Number).ToHashSet());
            await _waiting.Release(view.Event.Id, snapshotGroup, receipts);
            Rebuild();
        }

        await AskAgainWhenRefused(receipts);
        SnapshotReceipts.RefuseWhenNotRecorded(view, receipts);
        return recorded.Count > 0;
    }

    /// <summary>The group of the history that was sent last as that kind of time for the start number: the one whose entry can be corrected.</summary>
    SnapshotGroup? LatestOf(int number, SnapshotType type)
    {
        return _history.LastOrDefault(x => x.Type == type && x.Entries.Any(y => y.Number == number));
    }

    /// <summary>The time the server took is what the history that is kept shows too; one that cannot be kept is shown by this page still.</summary>
    async Task KeepTimeInHistory(SnapshotGroup group, Snapshot entry)
    {
        try
        {
            await _userSessionService.UpdateSnapshotInHistory(group.Id, entry.Number, entry.Timestamp!);
        }
        catch
        {
            // The server has the time, which is what matters; the history of this page shows it as it is held in memory.
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
    /// Whether a group that waits for the Event the view shows may be sent: a group is only sent on the authority of the Event it is of,
    /// and not while the person may not.
    /// </summary>
    bool CanResend(IViewedEvent view)
    {
        return view.CanWrite && _waiting.HasWaitingFor(view.Event.Id);
    }

    /// <summary>
    /// Starts the loop that sends the waiting groups again, on the authority of the view it starts with: a loop that is running keeps
    /// the view it began with, so that the group of another Event is not sent by it.
    /// </summary>
    void StartResending(IViewedEvent view)
    {
        if (_resending.IsCompleted)
        {
            _resending = _groups.RetryDurably(() => CanResend(view), token => ResendRound(view, token), _stopped.Token);
        }
    }

    /// <summary>
    /// The groups the device kept for the person and the Event are selected again with the times they were made with. A group that is
    /// already here is not added twice, and the page goes on with what the session holds when nothing can be read.
    /// </summary>
    async Task RestoreUnanswered()
    {
        await _account.Load();
        if (_socketContext.Event is not { } selected)
        {
            _waiting.Clear();
            return;
        }

        foreach (var group in await _waiting.Restore(selected.Id))
        {
            foreach (var entry in group.Entries)
            {
                // What is sent is what was kept: the time it was captured at takes the place of what the session had of it.
                _snapshots.RemoveAll(x => x.Number == entry.Number);
                _snapshots.Add(CopySnapshot(entry));
            }
        }
    }

    void SaveSelections()
    {
        _selections.Save(_snapshots.Select(CopySnapshot).ToArray());
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
}
