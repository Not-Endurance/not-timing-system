using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;

namespace NTS.Witness.Services;

public interface IPerformanceService : ISingleton
{
    IEnumerable<Person> GetPeople();
    Participation GetParticipationBy(Person person);
}
