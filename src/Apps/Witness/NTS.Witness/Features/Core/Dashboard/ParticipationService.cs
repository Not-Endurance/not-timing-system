using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Collections;
using Not.Injection;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Objects;

namespace NTS.Witness.Features.Core.Dashboard;

public class ParticipationService
    : NStatefulService<ObservableList<Participation>>,
        IParticipationService,
        IPerformanceService,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        ISingleton
{
    readonly IReadMany<Participation> _participationReader;
    Participation? _selected;

    public ParticipationService(IReadMany<Participation> participationReader)
    {
        _participationReader = participationReader;
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
        var participations = await _participationReader.ReadMany();
        State.ClearAndAddRange(participations);
        return State.Any();
    }

    public void Update(Participation participation, NCollectionAction action)
    {
        State.Update(participation, action);
    }

    public IEnumerable<Person> GetPeople()
    {
        return Participations.Select(p => p.Combination.Athlete.Names).Distinct();
    }

    public Participation GetParticipation(Person person)
    {
        return Participations.First(p => p.Combination.Athlete.Names.Equals(person));
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        var participation = notification.Participation;
        if (participation.Phases.Current.IsComplete() && participation.Phases.Current.IsFinal)
        {
            Update(participation, NCollectionAction.Remove);
            return Task.CompletedTask;
        }

        Update(participation, NCollectionAction.AddOrUpdate);
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
}
