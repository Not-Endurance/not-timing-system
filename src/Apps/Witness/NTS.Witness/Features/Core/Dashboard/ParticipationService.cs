using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Collections;
using Not.Exceptions;
using Not.Injection;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Watcher;
using NTS.Witness.Features.Sessions;

namespace NTS.Witness.Features.Core.Dashboard;

public class ParticipationService
    : NStatefulService<ObservableList<Participation>>,
        IParticipationService,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly IReadMany<Participation> _participationReader;
    readonly IUserSessionService _userSessionService;
    Participation? _selected;

    public ParticipationService(IReadMany<Participation> participationReader, IUserSessionService userSessionService)
    {
        _participationReader = participationReader;
        _userSessionService = userSessionService;
    }

    public Participation? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            EmitChanged();
        }
    }

    public IReadOnlyList<Participation> Participations => State;
    public IReadOnlyList<int> RecentlyTimed { get; } = [];
    public ObservableList<SnapshotGroup> History { get; } = [];

    protected override async Task<bool> InitializeState()
    {
        var participations = await _participationReader.ReadMany(x => !x.IsComplete() && !x.IsEliminated());
        var session = await _userSessionService.GetCurrent();
        State.ClearAndAddRange(participations);
        History.ClearAndAddRange(session?.SnapshotHistory ?? []);
        return State.Any() || History.Any();
    }

    public void Update(Participation participation, NCollectionAction action)
    {
        State.Update(participation, action);
        if (_selected == null)
        {
            return;
        }

        _selected = State.FirstOrDefault(x => x.Id == _selected.Id);
    }

    public async Task AppendHistory(SnapshotGroup snapshotGroup, int? eventId = null)
    {
        GuardHelper.ThrowIfDefault(snapshotGroup);

        History.AddOrReplace(snapshotGroup);
        EmitChanged();
        await _userSessionService.AppendSnapshot(snapshotGroup, eventId);
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
        _selected = null;
        History.Clear();
        State.Clear();
        ClearState();
        return Task.CompletedTask;
    }
}
