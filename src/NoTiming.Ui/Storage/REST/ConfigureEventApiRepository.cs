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
/// A Tenant Root makes an Event with a name, a location and a FEI show ID, and the Main Operator configures the rest of it
/// until it starts: so a Setup that is made with more than that is made, and then changed to the rest. The Tenant and the
/// Main Operator are the Api's to set and are not sent. An update announces itself only when the Api took it.
/// </summary>
public class ConfigureEventApiRepository : JsonApiRepository<ConfigureEvent, ConfigureEventModel>
{
    static readonly string[] MADE_WITH = ["name", "location", "feiShowId"];

    readonly IDomainEventDispatcher _domainEventDispatcher;

    public ConfigureEventApiRepository(JsonApiClient client, IDomainEventDispatcher domainEventDispatcher)
        : base("configure-events", client, "mainOperatorId")
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    protected override IReadOnlyCollection<string> CreateMembers => MADE_WITH;

    protected override async Task<bool> CreateCore(ConfigureEvent item)
    {
        if (!await base.CreateCore(item))
        {
            return false;
        }

        return !HasConfiguration(MapModel(item)) || await base.UpdateCore(item);
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

    static bool HasConfiguration(ConfigureEventModel model)
    {
        return model.Competitions.Length > 0
            || model.Officials.Length > 0
            || model.Operators.Length > 0
            || model.Loops.Length > 0
            || model.Combinations.Length > 0;
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
