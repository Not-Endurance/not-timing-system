using Not.Application.Behinds.Adapters;
using Not.Async.Extensions;
using NTS.Application.Contracts.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Core;

public class EnduranceEventService : NStatefulService, IActiveEventsContext, IEnduranceEventService
{
    readonly IEnduranceEventRepository _enduranceEvents;
    List<EnduranceEvent> _activeEvents = [];

    public EnduranceEventService(IEnduranceEventRepository enduranceEvents)
    {
        _enduranceEvents = enduranceEvents;
    }

    protected override async Task<bool> InitializeState()
    {
        _activeEvents = await _enduranceEvents.ReadActive().ToList();
        return true;
    }

    public Task<IEnumerable<EnduranceEvent>> GetActive()
    {
        return _enduranceEvents.ReadActive();
    }

    public Task<IEnumerable<EnduranceEvent>> GetPast()
    {
        return _enduranceEvents.ReadPast();
    }

    public bool IsActive(UpcomingEvent upcomingEvent)
    {
        return _activeEvents.Any(x => x.Id == upcomingEvent.Id);
    }

    // TODO: Create and consume ICache<EnduranceEvent> with TTL and invalidate method.
    // Where should this cache live? Probably shared as it might be useful in both app and UI layers
    // But usege of the cache should be explicit so that we know that item is cached and has to be invalidated
    // When values are manipulated. Consumers shouldn't care or know how the cache is repopulated.
    public void Add(EnduranceEvent enduranceEvent)
    {
        _activeEvents.RemoveAll(x => x.Id == enduranceEvent.Id);
        _activeEvents.Add(enduranceEvent);
        EmitChanged();
    }

    public void Remove(int eventId)
    {
        _activeEvents.RemoveAll(x => x.Id == eventId);
        EmitChanged();
    }
}
