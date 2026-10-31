using Not.Application.DomainEvents;
using Not.Application.HTTP;
using Not.Blazor.Client.REST;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Events;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// The Setup of an Event as the Api serves it (#603): JSON:API documents at <c>/api/configure-events</c> (ADR-0008, ADR-0012).
/// A Setup is made with everything it holds in one request, by Root in the Tenant it acts in. The Tenant is the Api's to set
/// and is not sent. An update announces itself only when the Api took it.
/// </summary>
public class ConfigureEventApiRepository : JsonApiRepository<ConfigureEvent, ConfigureEventModel>
{
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public ConfigureEventApiRepository(JsonApiClient client, IDomainEventDispatcher domainEventDispatcher)
        : base("configure-events", client)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    /// <summary>Makes a Setup with what it holds, in the Tenant the caller acts in. The Api answers with the id it made.</summary>
    public async Task<Guid> Make(ConfigureEventDraft draft)
    {
        var response = await Taken(Client.Post(Collection, draft));
        return Guid.Parse(ResourceOf(response).GetProperty("id").GetString()!);
    }

    public override async Task Update(ConfigureEvent item)
    {
        await base.Update(item);
        await DispatchUpdated(item);
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

/// <summary>What a Setup is made with: its name, its location and the FEI show id, when it has one. The Api makes the rest.</summary>
public class ConfigureEventDraft
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? FeiShowId { get; set; }
}
