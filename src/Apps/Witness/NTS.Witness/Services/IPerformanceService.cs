using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;

namespace NTS.Witness.Services;

public interface IPerformanceService : IStatefulService
{
    IEnumerable<Person> GetPeople();
    Participation GetParticipation(Person person);
}
