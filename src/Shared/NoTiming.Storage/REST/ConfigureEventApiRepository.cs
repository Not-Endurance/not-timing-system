using NC.Application.DomainEvents;
using NC.Application.HTTP;
using NC.Storage.REST;
using NC.Structures;
using NoTiming.Application.Contracts.Setup;
using NoTiming.Application.Contracts.Setup.Models;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Domain.Setup.Events;

namespace NoTiming.Storage.REST;

public class ConfigureEventApiRepository : ApiRepository<ConfigureEvent, ConfigureEventModel>
{
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public ConfigureEventApiRepository(NHttpClient httpClient, IDomainEventDispatcher domainEventDispatcher)
        : base("configure-event", httpClient)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    protected override async Task<Result<ConfigureEventModel>> UpdateCore(ConfigureEvent item)
    {
        var result = await base.UpdateCore(item);
        await DispatchUpdated(item);
        return result;
    }

    Task DispatchUpdated(ConfigureEvent item)
    {
        if (item.Id <= 0)
        {
            return Task.CompletedTask;
        }

        return _domainEventDispatcher.Dispatch(new ConfigureEventUpdated(item.Id));
    }
}
