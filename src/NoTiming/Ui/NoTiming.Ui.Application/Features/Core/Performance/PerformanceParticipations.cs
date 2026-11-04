using MediatR;
using NC.Application.Behinds.Adapters;
using NC.Collections;
using NC.Injection;
using NC.Observables.Structures;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Events;
using NoTiming.Domain.Core.Objects.Payloads;
using NoTiming.Ui.Contracts.Features.Performance;

namespace NoTiming.Ui.Application.Features.Core.Performance;

// TODO: Simplify the participation flow for snapshoting and probably remove this
public class PerformanceParticipations
    : NStatefulService<ObservableList<Participation>>,
        IPerformanceParticipations,
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
    readonly INoTimingSocketContext _socketContext;

    public PerformanceParticipations(
        IEventScopedRepository<Participation> participationReader,
        INoTimingSocketContext socketContext
    )
    {
        _participationReader = participationReader;
        _socketContext = socketContext;
    }

    public IReadOnlyList<Participation> Participations => State;

    protected override async Task<bool> InitializeState()
    {
        if (_socketContext.Event == null)
        {
            State.Clear();
            return false;
        }

        var participations = await _participationReader.ReadMany();
        State.ClearAndAddRange(participations);
        return State.Any();
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation);
        return Task.CompletedTask;
    }

    public Task Handle(InspectionRequired notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation);
        return Task.CompletedTask;
    }

    public Task Handle(RepresentationRequired notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation);
        return Task.CompletedTask;
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        State.Clear();
        ClearState();
        return Task.CompletedTask;
    }

    void Update(Participation participation)
    {
        State.Update(participation, NCollectionAction.AddOrUpdate);
    }
}
