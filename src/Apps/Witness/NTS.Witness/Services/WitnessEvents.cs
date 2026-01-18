using Not.Application.CRUD.Ports;
using Not.Storage.REST;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Services;

public class WitnessEvents : IWitnessEvents
{
    readonly IRepository<UpcomingEvent> _eventsRepository;

    public WitnessEvents(IRepository<UpcomingEvent> eventsRepository)
    {
        _eventsRepository = eventsRepository;
    }

    public async Task<IEnumerable<UpcomingEvent>> Get()
    {
        return await _eventsRepository.ReadMany();
    }
}
