using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Services;

public interface IWitnessEvents : ISingleton
{
    Task<IEnumerable<UpcomingEvent>> Get();
}
