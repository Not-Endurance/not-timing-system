using Not.Application.DomainEvents;
using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Events;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// The Setup of an Event as the Api serves it (#603): JSON:API documents at <c>/api/configure-events</c> (ADR-0008, ADR-0012).
/// A Setup is made with everything it holds in one request. The Tenant and the Main Operator are the Api's to set and are
/// not sent. An update announces itself only when the Api took it.
/// </summary>
public class ConfigureEventApiRepository : JsonApiRepository<ConfigureEvent, ConfigureEventModel>
{
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public ConfigureEventApiRepository(JsonApiClient client, IDomainEventDispatcher domainEventDispatcher)
        : base("configure-events", client, "mainOperatorId")
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    protected override async Task<bool> UpdateCore(ConfigureEvent item)
    {
        var updated = await base.UpdateCore(item);
        if (updated)
        {
            await DispatchUpdated(item);
        }

        return updated;
    }

    Task DispatchUpdated(ConfigureEvent item)
    {
        if (item.Id == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        return _domainEventDispatcher.Dispatch(new ConfigureEventUpdated(item.Id));
    }
}
