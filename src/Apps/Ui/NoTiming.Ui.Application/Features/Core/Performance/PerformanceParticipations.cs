using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Collections;
using Not.Injection;
using Not.Observables.Structures;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Witness.Contracts.Features.Performance;

namespace NTS.Witness.Features.Core.Performance;

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
    readonly INtsSocketContext _socketContext;

    public PerformanceParticipations(
        IEventScopedRepository<Participation> participationReader,
        INtsSocketContext socketContext
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
