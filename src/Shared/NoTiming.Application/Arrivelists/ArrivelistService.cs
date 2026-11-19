using MediatR;
using NC.Application.Behinds.Adapters;
using NC.Collections;
using NoTiming.Application.Contracts.Arrivelists;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Core.Events;
using NoTiming.Domain.Core.Objects.Arrivelists;

namespace NoTiming.Application.Arrivelists;

public class ArrivelistService
    : NStatefulService,
        IArrivelistService,
        INotificationHandler<ParticipationChanged>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>
{
    readonly IEventScopedRepository<Participation> _participations;
    readonly INoTimingSocketContext? _socketContext;
    UniqueParticipations _state = new();

    public ArrivelistService(IEventScopedRepository<Participation> participations)
        : this(participations, null) { }

    public ArrivelistService(
        IEventScopedRepository<Participation> participations,
        INoTimingSocketContext? socketContext
    )
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

    public Task Handle(ParticipationChanged notification, CancellationToken cancellationToken)
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
