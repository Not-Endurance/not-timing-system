using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Contracts.Core;

public interface IActiveEventsContext : IStatefulService
{
    bool IsActive(UpcomingEvent upcomingEvent);
    void Add(EnduranceEvent enduranceEvent);
    void Remove(int eventId);
}
