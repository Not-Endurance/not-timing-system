using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Collections;
using NTS.Application.Contracts.Arrivelists;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Arrivelists;
using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Application.Arrivelists;

public class ArrivelistService
    : NStatefulService,
        IArrivelistService,
        INotificationHandler<ParticipationArrived>,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationRestored>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>
{
    readonly IEventScopedRepository<Participation> _participations;
    readonly INtsSocketContext? _socketContext;
    UniqueParticipations _state = new();

    public ArrivelistService(IEventScopedRepository<Participation> participations)
        : this(participations, null) { }

    public ArrivelistService(IEventScopedRepository<Participation> participations, INtsSocketContext? socketContext)
    {
        _participations = participations;
        _socketContext = socketContext;
    }

    public Arrivelist Arrivelist { get; private set; } = new([]);
    public IReadOnlyList<ArrivelistEntry> Entries => Arrivelist.Entries;

    protected override async Task<bool> InitializeState()
    {
        if (_socketContext?.Event == null && _socketContext != null)
        {
            _state.Clear();
            Arrivelist = new Arrivelist([]);
            return false;
        }

        var participations = await _participations.ReadMany(x => !x.IsComplete() && !x.IsEliminated());
        _state = new UniqueParticipations(participations);
        Arrivelist = new Arrivelist(_state);
        return Entries.Any();
    }

    public Task Handle(ParticipationArrived notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.Remove);
        return Task.CompletedTask;
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        _state.Clear();
        Arrivelist = new Arrivelist([]);
        ClearState();
        return Task.CompletedTask;
    }

    public void Tick()
    {
        Arrivelist = new Arrivelist(_state);
        EmitChanged();
    }

    void Update(Participation participation, NCollectionAction action)
    {
        switch (action)
        {
            case NCollectionAction.AddOrUpdate:
                if (!participation.IsComplete() && !participation.IsEliminated())
                {
                    _state.Upsert(participation);
                }
                else
                {
                    _state.Remove(participation);
                }
                break;
            case NCollectionAction.Remove:
                _state.Remove(participation);
                break;
        }

        Arrivelist = new Arrivelist(_state);
        EmitChanged();
    }
}
