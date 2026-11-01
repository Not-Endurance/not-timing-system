using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.Snapshots;

/// <summary>
/// The rows an Update of a Snapshot is about, for the endpoint filter (#671): the id of the route is the id of a time event,
/// and the Participation that holds it is the row, which says which Event the Snapshot is of.
/// </summary>
internal sealed class SnapshotRows : IEventRows
{
    public async Task<IEventScoped?> FindAsync(CrossTenantReads reads, Guid id, CancellationToken cancellationToken)
    {
        return await reads.FindParticipationOfTimeEventAsync(id, cancellationToken);
    }
}
