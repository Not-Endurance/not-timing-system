using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// Which rows an endpoint is about, for the routes whose place is told by a row (<see cref="PlaceIn.EventOfRow"/>): the row
/// with the id of the route, in whatever Tenant it is, belongs to an Event, and that Event is the place. The endpoint filter
/// finds the row once, with this, and hands it to the handler with the rest of what it found. It is metadata of the endpoint.
/// </summary>
internal interface IEventRows
{
    /// <summary>The row with the id, whichever Tenant it is in; none when there is none.</summary>
    Task<IEventScoped?> FindAsync(CrossTenantReads reads, Guid id, CancellationToken cancellationToken);
}
