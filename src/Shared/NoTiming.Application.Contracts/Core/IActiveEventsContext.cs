using NC.Application.Behinds.Adapters;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Application.Contracts.Core;

public interface IActiveEventsContext : IStatefulService
{
    bool IsActive(ConfigureEvent configureEvent);
    void Add(EventInformation eventInformation);
    void Remove(int eventId);
}
