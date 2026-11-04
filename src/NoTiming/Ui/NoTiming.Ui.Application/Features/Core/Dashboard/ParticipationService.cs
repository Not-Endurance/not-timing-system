using MediatR;
using NC.Application.Behinds.Adapters;
using NC.Collections;
using NC.Injection;
using NC.Observables.Structures;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Events;
using NoTiming.Domain.Core.Objects.Payloads;

namespace NoTiming.Ui.Application.Features.Core.Dashboard;

public class ParticipationService
    : NStatefulService<ObservableList<Participation>>,
        IParticipationContext,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<InspectionRequired>,
        INotificationHandler<RepresentationRequired>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly IEventScopedRepository<Participation> _participationReader;
    readonly INoTimingSocketContext? _socketContext;
    Participation? _selected;

    public ParticipationService(IEventScopedRepository<Participation> participationReader)
        : this(participationReader, null) { }

    public ParticipationService(
        IEventScopedRepository<Participation> participationReader,
        INoTimingSocketContext? socketContext
    )
    {
        _participationReader = participationReader;
        _socketContext = socketContext;
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

    protected override async Task<bool> InitializeState()
    {
        if (_socketContext?.Event == null && _socketContext != null)
        {
            State.Clear();
            return false;
        }

        var participations = await _participationReader.ReadMany(x => !x.IsComplete() && !x.IsEliminated());
        State.ClearAndAddRange(participations);
        return State.Any();
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(InspectionRequired notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(RepresentationRequired notification, CancellationToken cancellationToken)
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
        State.Clear();
        ClearState();
        return Task.CompletedTask;
    }

    void Update(Participation participation, NCollectionAction action)
    {
        State.Update(participation, action);
        if (_selected == null)
        {
            return;
        }

        _selected = State.FirstOrDefault(x => x.Id == _selected.Id);
    }
}
