using Not.Application.HTTP;
using Not.Blazor.Client.REST;
using NTS.Contracts.Access.Models;
using NTS.Domain.Access;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// The grants of an Event as the Api serves them (ADR-0012): the people the Console Operator linked to its Officials and
/// Operators, at <c>/api/event-grants</c>. A grant is read as its model, since the Api names its display name and whether it
/// is pending, and neither is in the entity.
/// </summary>
public class EventGrantApiRepository : JsonApiRepository<EventGrant, EventGrantModel>
{
    public EventGrantApiRepository(JsonApiClient client)
        : base("event-grants", client) { }

    /// <summary>The grants of the Event, as the Api lists them.</summary>
    public Task<IReadOnlyList<EventGrantModel>> ListOf(Guid eventId)
    {
        return ModelsWithin(x => x.EventId == eventId);
    }

    /// <summary>Links a person to the Event. The Api answers with the grant it made, which is read as its model.</summary>
    public async Task<EventGrantModel> Link(EventGrantModel grant)
    {
        var response = await Taken(Client.Post(Collection, grant));
        return ModelOf(ResourceOf(response))
            ?? throw new InvalidOperationException("The Api made a grant that cannot be read.");
    }
}
