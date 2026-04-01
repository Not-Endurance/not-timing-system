using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Collections;
using Not.Exceptions;
using Not.Injection;
using Not.Observables.Structures;
using NTS.Application.Socket;
using NTS.Application.UserSession;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Enums;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;

namespace NTS.Witness.Features.Core.Dashboard;

public class SnapshotService
    : NStatefulService<ObservableList<Participation>>,
        ISnapshotService,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly Dictionary<int, Participation> _allParticipations = [];
    readonly List<SnapshotGroup> _history = [];
    readonly INtsSocketContext _socketContext;
    readonly IReadMany<Participation> _participationReader;
    readonly List<Participation> _participationsToSnapshot = [];
    readonly ISnapshotPublisher _snapshotPublisher;
    readonly List<Snapshot> _snapshots = [];
    readonly IWitnessUserSession _userSessionService;

    public SnapshotService(
        INtsSocketContext socketContext,
        IReadMany<Participation> participationReader,
        IWitnessUserSession userSessionService,
        ISnapshotPublisher snapshotPublisher
    )
    {
        _socketContext = socketContext;
        _participationReader = participationReader;
        _userSessionService = userSessionService;
        _snapshotPublisher = snapshotPublisher;
    }

    public ObservableList<Participation> Participations => State;
    public IReadOnlyList<Participation> ParticipationsToSnapshot => _participationsToSnapshot;
    public IReadOnlyList<Snapshot> Snapshots => _snapshots;
    public IReadOnlyList<SnapshotGroup> History => _history;

    protected override async Task<bool> InitializeState()
    {
        if (_socketContext.Event == null)
        {
            return false;
        }

        var participations = await _participationReader.ReadMany(x => !x.IsComplete() && !x.IsEliminated());
        var session = await _userSessionService.GetCurrent();

        _allParticipations.Clear();
        foreach (var participation in participations)
        {
            _allParticipations[participation.Combination.Number] = participation;
        }

        _history.Clear();
        _history.AddRange(session?.GetSnapshotHistory() ?? []);
        Participations.ClearAndAddRange(participations);

        return Participations.Any() || _participationsToSnapshot.Any() || _history.Any();
    }

    public void CaptureSnapshot(Snapshot snapshot)
    {
        GuardHelper.ThrowIfDefault(snapshot);
        UpdateSnapshotTimestamp(snapshot, new Timestamp(DateTimeOffset.Now));
    }

    public void MoveToSnapshot(Participation participation)
    {
        GuardHelper.ThrowIfDefault(participation);

        if (_participationsToSnapshot.Any(x => x.Id == participation.Id))
        {
            return;
        }

        _participationsToSnapshot.Add(participation);
        _snapshots.Add(new Snapshot(participation.Combination.Number, participation.Combination.Athlete.Names));
        Participations.Remove(participation);
        EmitChanged();
    }

    public void RemoveSnapshot(Snapshot snapshot)
    {
        GuardHelper.ThrowIfDefault(snapshot);

        if (_snapshots.All(x => x.Number != snapshot.Number))
        {
            return;
        }

        FlushSnapshots([snapshot.Number]);
        EmitChanged();
    }

    public async Task<bool> Publish(SnapshotType snapshotType)
    {
        var readySnapshots = _snapshots.Where(x => x.Timestamp != null).ToList();
        if (readySnapshots.Count == 0)
        {
            return false;
        }

        var snapshotGroup = new SnapshotGroup(readySnapshots, snapshotType);
        await _snapshotPublisher.PublishSnapshotsAsync(snapshotGroup);
        await _userSessionService.AppendSnapshot(snapshotGroup);

        _history.Add(snapshotGroup);
        FlushSnapshots(readySnapshots.Select(x => x.Number).ToHashSet());
        EmitChanged();
        return true;
    }

    public async Task RePublish(SnapshotGroup snapshotGroup, SnapshotType snapshotType)
    {
        GuardHelper.ThrowIfDefault(snapshotGroup);
        var snapshotGroupToPublish = new SnapshotGroup(snapshotGroup.Entries, snapshotType);
        await _snapshotPublisher.PublishSnapshotsAsync(snapshotGroupToPublish);
    }

    public void UpdateSnapshotTimestamp(Snapshot snapshot, Timestamp timestamp)
    {
        GuardHelper.ThrowIfDefault(snapshot);
        GuardHelper.ThrowIfDefault(timestamp);

        var existingSnapshot = _snapshots.FirstOrDefault(x => x.Number == snapshot.Number);
        if (existingSnapshot == null)
        {
            return;
        }

        existingSnapshot.Timestamp = timestamp;
        EmitChanged();
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.Remove);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        _allParticipations.Clear();
        _history.Clear();
        _participationsToSnapshot.Clear();
        _snapshots.Clear();
        Participations.Clear();
        ClearState();
        return Task.CompletedTask;
    }

    void FlushSnapshots(HashSet<int> participationNumbers)
    {
        if (participationNumbers.Count == 0)
        {
            return;
        }

        _participationsToSnapshot.RemoveAll(x => participationNumbers.Contains(x.Combination.Number));
        _snapshots.RemoveAll(x => participationNumbers.Contains(x.Number));

        foreach (var number in participationNumbers)
        {
            if (
                _allParticipations.TryGetValue(number, out var participation)
                && Participations.All(x => x.Id != participation.Id)
            )
            {
                Participations.AddOrReplace(participation);
            }
        }
    }

    void Update(Participation participation, NCollectionAction action)
    {
        var number = participation.Combination.Number;
        switch (action)
        {
            case NCollectionAction.AddOrUpdate:
                _allParticipations[number] = participation;
                break;
            case NCollectionAction.Remove:
                _allParticipations.Remove(number);
                break;
        }

        if (_participationsToSnapshot.Any(x => x.Combination.Number == number))
        {
            return;
        }

        Participations.Update(participation, action);
    }
}
