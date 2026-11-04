using Not.Application.DomainEvents;
using Not.Application.HTTP;
using Not.Storage.REST;
using Not.Structures;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Events;

namespace NTS.Storage.REST;

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
