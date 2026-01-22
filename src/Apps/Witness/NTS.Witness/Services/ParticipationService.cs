using Not.Application.Behinds.Adapters;
using Not.Collections;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;

namespace NTS.Witness.Services;

public class ParticipationService
    : NStatefulService<ObservableList<Participation>>,
        IParticipationService,
        IClientParticipationUpdate,
        IPerformanceService
{
    IEnumerable<Participation> Participations => State;

    public IEnumerable<Participation> ActiveParticipations
    {
        get => Participations;
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
        Participations.ToList().Update(participation, action);
        if (action == NCollectionAction.AddOrUpdate)
        {
            State.AddOrReplace(participation);
        }
        else if (action == NCollectionAction.Remove)
        {
            State.Remove(participation);
        }
    }

    public IEnumerable<Person> GetPeople()
    {
        return Participations.Select(p => p.Combination.Athlete.Names).Distinct();
    }

    public Participation GetParticipationBy(Person person)
    {
        return Participations.First(p => p.Combination.Athlete.Names.Equals(person));
    }
}
