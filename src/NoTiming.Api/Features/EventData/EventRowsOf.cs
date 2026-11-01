using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// The rows of one family of what an Event keeps, for the endpoint filter (#671): a change or a removal is about the row with
/// the id of the route, and the Event of the row is where the caller has to be allowed to run it.
/// </summary>
internal sealed class EventRowsOf<TModel> : IEventRows
    where TModel : class, IDocument, IEventScoped
{
    readonly string _collection;

    public EventRowsOf(string collection)
    {
        _collection = collection;
    }

    public async Task<IEventScoped?> FindAsync(CrossTenantReads reads, Guid id, CancellationToken cancellationToken)
    {
        return await reads.FindEventRowAsync<TModel>(_collection, id, cancellationToken);
    }
}
