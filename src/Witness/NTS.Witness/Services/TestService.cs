using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;
using NTS.Witness.Blazor.Components.Performances;
using NTS.Witness.Blazor.Components.Snapshots;

namespace NTS.Witness.Services;

public class TestService : IPerformanceService, ISnapshotService
{
    public List<Participation> GetParticipations()
    {
        return DummyData.CreateParticipationsForSnapshot(10);
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
