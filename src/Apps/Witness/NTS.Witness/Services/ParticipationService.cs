using Not.Application.Behinds.Adapters;
using Not.Collections;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Objects.Documents;
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
        State.AddRange(Participations);
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

    public Person GetPerson()
    {
        return DummyData.CreateParticipant("Pesho", "Goshov");
    }

    public List<Phase> GetPhases()
    {
        return DummyData.CreatePhases();
    }
}
