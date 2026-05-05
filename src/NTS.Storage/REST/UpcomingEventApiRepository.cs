using Not.Application.DomainEvents;
using Not.Application.HTTP;
using Not.Storage.REST;
using Not.Structures;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Events;

namespace NTS.Storage.REST;

public class UpcomingEventApiRepository : ApiRepository<UpcomingEvent, UpcomingEventModel>
{
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public UpcomingEventApiRepository(NHttpClient httpClient, IDomainEventDispatcher domainEventDispatcher)
        : base("upcoming-event", httpClient)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    protected override async Task<Result<UpcomingEventModel>> UpdateCore(UpcomingEvent item)
    {
        var result = await base.UpdateCore(item);
        await DispatchUpdated(item);
        return result;
    }

    Task DispatchUpdated(UpcomingEvent item)
    {
        if (item.Id <= 0)
        {
            return Task.CompletedTask;
        }

        return _domainEventDispatcher.Dispatch(new UpcomingEventUpdated(item.Id));
    }
}
