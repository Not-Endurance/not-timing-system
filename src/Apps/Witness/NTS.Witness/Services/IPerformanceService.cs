using Not.Injection;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;

namespace NTS.Witness.Services;

public interface IPerformanceService : ISingleton
{
    Person GetPerson();
    List<Phase> GetPhases();
}
