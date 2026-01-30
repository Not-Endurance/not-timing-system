using Not.Injection;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Objects;

namespace NTS.Witness.Blazor.Components.Performances;

public interface IPerformanceService : ISingleton
{
    Person GetPerson();
    List<Phase> GetPhases();
}
