using MediatR;
using Not.Application.Behinds.Adapters;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Application.Contracts.Startlists;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

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
    static readonly IReadOnlyDictionary<int, IReadOnlyList<Starter>> EMPTY_BY_STAGE =
        new Dictionary<int, IReadOnlyList<Starter>>();

    readonly IEventScopedRepository<Participation> _participations;
    readonly INtsSocketContext? _socketContext;

    public StartlistService(IEventScopedRepository<Participation> participations)
        : this(participations, null) { }

    public StartlistService(
        IEventScopedRepository<Participation> participations,
        INtsSocketContext? socketContext
    )
    {
        _participations = participations;
        _socketContext = socketContext;
    }

    public Startlist? Startlist { get; set; }

    public IReadOnlyList<Starter> Upcoming => Startlist?.Upcoming ?? [];
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> UpcomingByStage =>
        Startlist?.UpcomingByStage ?? EMPTY_BY_STAGE;

    public IReadOnlyList<Starter> History => Startlist?.History ?? [];
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage =>
        Startlist?.HistoryByStage ?? EMPTY_BY_STAGE;

    protected override async Task<bool> InitializeState()
    {
        if (_socketContext?.Event == null && _socketContext != null)
        {
            Startlist = new Startlist([]);
            return false;
        }

        var participations = await _participations.ReadMany();
        Startlist = new Startlist(participations);
        return Startlist.History.Any() || Startlist.Upcoming.Any();
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        var participation = notification.Participation;
        Startlist?.Upsert(participation);

        if (participation.Phases.Current.IsComplete() && participation.Phases.Current.IsFinal)
        {
            Startlist?.Remove(participation.Combination.Number);
        }
        else
        {
            Startlist?.UpsertNext(participation);
        }

        EmitChanged();
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        if (!notification.Participation.Phases.Current.IsComplete())
        {
            Startlist?.UpsertCurrent(notification.Participation);
        }
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        Startlist?.Remove(notification.Participation.Combination.Number);
        EmitChanged();
        return Task.CompletedTask;
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        Startlist = new Startlist([]);
        ClearState();
        return Task.CompletedTask;
    }

    public void Refresh()
    {
        Startlist?.UpdateState();
        EmitChanged();
    }
}
