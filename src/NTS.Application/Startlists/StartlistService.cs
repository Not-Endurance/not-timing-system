using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

public class StartlistService
    : NStatefulService,
        IStartUpcoming,
        IStartHistory,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationRestored>,
        INotificationHandler<ParticipationEliminated>
{
    readonly IReadMany<Participation> _participations;

    public StartlistService(IReadMany<Participation> participations)
    {
        _participations = participations;
    }

    public Startlist? Startlist { get; set; }

    public IReadOnlyList<StartlistEntry> Upcoming => Startlist?.Upcoming ?? [];
    public IReadOnlyList<StartlistEntry> History => Startlist?.History ?? [];

    protected override async Task<bool> InitializeState()
    {
        var participations = await _participations.ReadMany();
        Startlist = new Startlist(participations);
        return Startlist.History.Any() || Startlist.Upcoming.Any();
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        var participation = notification.Participation;
        if (participation.Phases.Current.IsComplete() && participation.Phases.Current.IsFinal)
        {
            RemoveEntry(participation);
            return Task.CompletedTask;
        }

        AddEntry(participation);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        AddEntry(notification.Participation);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        RemoveEntry(notification.Participation);
        return Task.CompletedTask;
    }

    public void Refresh()
    {
        Startlist?.UpdateState();
        EmitChanged();
    }

    void RemoveEntry(Participation participation)
    {
        Startlist?.Remove(participation.Combination.Number);
        EmitChanged();
    }

    void AddEntry(Participation participation)
    {
        Startlist?.Add(participation);
        EmitChanged();
    }
}
