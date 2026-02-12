using Not.Application.Behinds.Adapters;
using Not.Collections;
using Not.Injection;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;

namespace NTS.Witness.Services;

public class ParticipationService
    : NStatefulService<ObservableList<Participation>>,
        IParticipationService,
        IPerformanceService,
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
}
