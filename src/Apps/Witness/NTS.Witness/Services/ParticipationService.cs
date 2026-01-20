using Not.Collections;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;

namespace NTS.Witness.Services;

public class ParticipationService : IPerformanceService, IParticipationService
{
    List<Participation> _participations = [];

    public IEnumerable<Participation> ActiveParticipations => _participations.AsEnumerable();

    public void Update(Participation participation, NCollectionAction action)
    {
        _participations.Update(participation, action);
    }

    public List<Participation> GetParticipations()
    {
        return DummyData.CreateParticipations(10);
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
