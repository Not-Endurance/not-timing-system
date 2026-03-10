using Not.Application.DomainEvents;
using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Objects.Payloads;

namespace NTS.Storage.REST;

public class UpcomingEventRestApiRepository : RestApiRepository<UpcomingEvent, UpcomingEventModel>, ITransient
{
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public UpcomingEventRestApiRepository(NHttpClient httpClient, IDomainEventDispatcher domainEventDispatcher)
        : base("upcoming-event", httpClient)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    protected override async Task InternalUpdate(UpcomingEvent item)
    {
        await base.InternalUpdate(item);
        await DispatchUpdated(item);
    }

    Task DispatchUpdated(UpcomingEvent item)
    {
        if (item.Id <= 0)
        {
            return Task.CompletedTask;
        }

        return _domainEventDispatcher.Dispatch(new UpcomingEventUpdatedEvent(item.Id));
    }
}
