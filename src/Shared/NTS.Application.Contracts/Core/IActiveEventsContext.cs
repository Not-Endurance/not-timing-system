using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Contracts.Core;

public interface IActiveEventsContext : IStatefulService
{
    bool IsActive(ConfigureEvent configureEvent);
    void Add(EventInformation eventInformation);
    void Remove(int eventId);
}
