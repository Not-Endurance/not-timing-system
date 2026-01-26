using Not.Application.Behinds.Adapters;
using Not.Collections;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;

namespace NTS.Witness.Services;

public class ParticipationService
    : NStatefulService<ObservableList<Participation>>,
        IParticipationContext,
        IParticipationUpdate,
        IPerformanceService
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

    protected override Task<bool> CreateState(params IEnumerable<object> arguments)
    {
        return Task.FromResult(true);
    }

    public void Update(Participation participation, NCollectionAction action)
    {
        State.Update(participation, action);
    }

    public IEnumerable<Person> GetPeople()
    {
        return State.Select(p => p.Combination.Athlete.Names).Distinct();
    }

    public Participation GetParticipationBy(Person person)
    {
        return State.First(p => p.Combination.Athlete.Names.Equals(person));
    }
}
