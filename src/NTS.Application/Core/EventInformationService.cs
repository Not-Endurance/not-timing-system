using Not.Application.Behinds.Adapters;
using Not.Async.Extensions;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Core;

public class EventInformationService : NStatefulService, ILiveEventsContext, IEventInformationService
{
    readonly IEventInformationRepository _eventInformation;
    List<EventInformation> _liveEvents = [];

    public EventInformationService(IEventInformationRepository eventInformation)
    {
        _eventInformation = eventInformation;
    }

    protected override async Task<bool> InitializeState()
    {
        _liveEvents = await _eventInformation.ReadLive().ToList();
        return true;
    }

    public Task<IEnumerable<EventInformation>> GetLive()
    {
        return _eventInformation.ReadLive();
    }

    public Task<IEnumerable<EventInformation>> GetHistoric()
    {
        return _eventInformation.ReadHistoric();
    }

    public bool IsLive(ConfigureEvent configureEvent)
    {
        return _liveEvents.Any(x => x.Id == configureEvent.Id);
    }

    // TODO: Create and consume ICache<EventInformation> with TTL and invalidate method.
    // Where should this cache live? Probably shared as it might be useful in both app and UI layers
    // But usege of the cache should be explicit so that we know that item is cached and has to be invalidated
    // When values are manipulated. Consumers shouldn't care or know how the cache is repopulated.
    public void Add(EventInformation eventInformation)
    {
        _liveEvents.RemoveAll(x => x.Id == eventInformation.Id);
        _liveEvents.Add(eventInformation);
        EmitChanged();
    }

    public void Remove(Guid eventId)
    {
        _liveEvents.RemoveAll(x => x.Id == eventId);
        EmitChanged();
    }
}
