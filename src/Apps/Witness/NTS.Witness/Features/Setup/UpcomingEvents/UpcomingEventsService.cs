using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Features.Setup.UpcomingEvents;

public class UpcomingEventsService : IUpcomingEventService, ITransient
{
    readonly IRepository<UpcomingEvent> _upcomingEventsz;

    public UpcomingEventsService(IRepository<UpcomingEvent> upcomingEventsz)
    {
        _upcomingEventsz = upcomingEventsz;
    }

    public async Task<IEnumerable<UpcomingEvent>> GetEvents()
    {
        return await _upcomingEventsz.ReadMany();
    }
}

public interface IUpcomingEventService
{
    Task<IEnumerable<UpcomingEvent>> GetEvents();
}
