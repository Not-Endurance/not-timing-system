using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Collections;
using Not.Injection;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Objects;

namespace NTS.Witness.Services;

public class ParticipationService
    : NStatefulService<ObservableList<Participation>>,
        IParticipationService,
        IPerformanceService,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        ISingleton
{
    public IEnumerable<Participation> Active
    {
        get => State;
        set
        {
            State.Clear();
            State.AddRange(value);
        }
    }

    public Participation? Selected { get; set; }
    public IReadOnlyList<Participation> Participations { get; set; } = [];
    public IReadOnlyList<int> RecentlyTimed { get; set; } = [];

    public void Update(Participation participation, NCollectionAction action)
    {
        State.Update(participation, action);
    }

    public IEnumerable<Person> GetPeople()
    {
        return State.Select(p => p.Combination.Athlete.Names).Distinct();
    }

    public Participation GetParticipation(Person person)
    {
        return State.First(p => p.Combination.Athlete.Names.Equals(person));
    }

    public void Set(IEnumerable<Participation> participations)
    {
        Participations = participations.ToList().AsReadOnly();
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
