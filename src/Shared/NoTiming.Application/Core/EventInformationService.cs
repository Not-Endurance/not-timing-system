using Not.Application.Behinds.Adapters;
using Not.Async.Extensions;
using NTS.Application.Contracts.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Core;

public class EventInformationService : NStatefulService, IActiveEventsContext, IEventInformationService
{
    readonly IEventInformationRepository _eventInformation;
    List<EventInformation> _activeEvents = [];

    public EventInformationService(IEventInformationRepository eventInformation)
    {
        _eventInformation = eventInformation;
    }

    protected override async Task<bool> InitializeState()
    {
        _activeEvents = await _eventInformation.ReadActive().ToList();
        return true;
    }

    public Task<IEnumerable<EventInformation>> GetActive()
    {
        return _eventInformation.ReadActive();
    }

    public Task<IEnumerable<EventInformation>> GetPast()
    {
        return _eventInformation.ReadPast();
    }

    public bool IsActive(ConfigureEvent configureEvent)
    {
        return _activeEvents.Any(x => x.Id == configureEvent.Id);
    }

    // TODO: Create and consume ICache<EventInformation> with TTL and invalidate method.
    // Where should this cache live? Probably shared as it might be useful in both app and UI layers
    // But usege of the cache should be explicit so that we know that item is cached and has to be invalidated
    // When values are manipulated. Consumers shouldn't care or know how the cache is repopulated.
    public void Add(EventInformation eventInformation)
    {
        _activeEvents.RemoveAll(x => x.Id == eventInformation.Id);
        _activeEvents.Add(eventInformation);
        EmitChanged();
    }

    public void Remove(int eventId)
    {
        _activeEvents.RemoveAll(x => x.Id == eventId);
        EmitChanged();
    }
}
