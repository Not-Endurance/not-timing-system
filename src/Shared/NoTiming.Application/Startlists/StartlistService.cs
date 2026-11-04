using MediatR;
using NC.Application.Behinds.Adapters;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Application.Contracts.Startlists;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Core.Events;
using NoTiming.Domain.Core.Objects.Payloads;
using NoTiming.Domain.Core.Objects.Startlists;

namespace NoTiming.Application.Startlists;

public class StartlistService
    : NStatefulService,
        IStartUpcoming,
        IStartHistory,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationRestored>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>
{
    readonly IEventScopedRepository<Participation> _participations;
    readonly INoTimingSocketContext? _socketContext;
    UniqueParticipations _state = new();

    public StartlistService(IEventScopedRepository<Participation> participations)
        : this(participations, null) { }

    public StartlistService(IEventScopedRepository<Participation> participations, INoTimingSocketContext? socketContext)
    {
        _participations = participations;
        _socketContext = socketContext;
    }

    public Startlist Startlist { get; private set; } = new([]);

    public IReadOnlyList<Starter> Upcoming => Startlist.Upcoming;

    public IReadOnlyList<Starter> History => Startlist.History;
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage => Startlist.HistoryByStage;

    protected override async Task<bool> InitializeState()
    {
        if (_socketContext?.Event == null && _socketContext != null)
        {
            _state.Clear();
            Startlist = new Startlist([]);
            return false;
        }

        var participations = await _participations.ReadMany();
        _state = new UniqueParticipations(participations);
        Startlist = new Startlist(_state);
        return Startlist.History.Any() || Startlist.Upcoming.Any();
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation);
        return Task.CompletedTask;
    }

    public async Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        await Refresh(notification.Participation);
    }

    public async Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        await Refresh(notification.Participation);
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        _state.Clear();
        Startlist = new Startlist([]);
        ClearState();
        return Task.CompletedTask;
    }

    public void Tick()
    {
        Startlist = new Startlist(_state);
        EmitChanged();
    }

    void Update(Participation participation)
    {
        _state.Upsert(participation);
        Startlist = new Startlist(_state);
        EmitChanged();
    }

    async Task Refresh(Participation participation)
    {
        var persisted = await _participations.Read(participation.Id);
        Update(persisted ?? participation);
    }
}
