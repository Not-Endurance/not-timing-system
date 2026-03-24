using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Async.Extensions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Core;

public class EnduranceEventService : NStatefulService, IEnduranceEventService
{
    readonly IRepository<EnduranceEvent> _enduranceEvents;
    List<EnduranceEvent> _activeEvents = [];

    public EnduranceEventService(IRepository<EnduranceEvent> enduranceEvents)
    {
        _enduranceEvents = enduranceEvents;
    }

    protected override async Task<bool> InitializeState()
    {
        _activeEvents = await _enduranceEvents.ReadMany().ToList();
        return true;
    }

    public Task<IEnumerable<EnduranceEvent>> GetEvents()
    {
        return _enduranceEvents.ReadMany();
    }

    public bool IsActive(UpcomingEvent upcomingEvent)
    {
        return _activeEvents.Any(x => x.Id == upcomingEvent.Id);
    }
}

public interface IEnduranceEventService : IStatefulService
{
    bool IsActive(UpcomingEvent upcomingEvent);
    Task<IEnumerable<EnduranceEvent>> GetEvents();
}
