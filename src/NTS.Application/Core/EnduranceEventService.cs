using Not.Application.CRUD.Ports;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Core;

public class EnduranceEventService : IEnduranceEventService
{
    readonly IRepository<EnduranceEvent> _enduranceEvents;

    public EnduranceEventService(IRepository<EnduranceEvent> enduranceEvents)
    {
        _enduranceEvents = enduranceEvents;
    }

    public Task<IEnumerable<EnduranceEvent>> GetEvents()
    {
        return _enduranceEvents.ReadMany();
    }
}

public interface IEnduranceEventService
{
    Task<IEnumerable<EnduranceEvent>> GetEvents();
}
