using Not.Application.HTTP;
using Not.Blazor.Client.REST;
using NTS.Contracts.Access.Models;
using NTS.Domain.Access;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// The Console Operators of an Event as the Api serves them (ADR-0012): the grants of the kind Console Operator, at
/// <c>/api/console-operators</c>. A Console Operator is assigned by the account of a person, and removed by its id.
/// </summary>
public class ConsoleOperatorApiRepository : JsonApiRepository<EventGrant, ConsoleOperatorModel>
{
    public ConsoleOperatorApiRepository(JsonApiClient client)
        : base("console-operators", client) { }

    /// <summary>The Console Operators of the Event, by their email, as the Api lists them.</summary>
    public Task<IReadOnlyList<ConsoleOperatorModel>> ListOf(Guid eventId)
    {
        return ModelsWithin(x => x.EventId == eventId, sort: "email");
    }

    /// <summary>Assigns the account to the Event as a Console Operator.</summary>
    public async Task Assign(Guid eventId, Guid accountId)
    {
        await Taken(Client.Post(Collection, new ConsoleOperatorModel { EventId = eventId, AccountId = accountId }));
    }
}
