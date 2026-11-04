using NC.Application.HTTP;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Storage.REST;

namespace NoTiming.Storage.Core.Repositories;

public class OfficialEventScopedApiRepository : EventScopedApiRepository<Official, OfficialModel>
{
    public OfficialEventScopedApiRepository(NHttpClient client, EventScopeFactory<Official> eventScopeFactory)
        : base("officials", client, eventScopeFactory) { }
}
