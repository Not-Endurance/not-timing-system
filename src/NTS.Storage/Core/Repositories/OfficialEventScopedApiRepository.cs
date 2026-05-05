using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class OfficialEventScopedApiRepository : EventScopedApiRepository<Official, OfficialModel>, ITransient
{
    public OfficialEventScopedApiRepository(NHttpClient client, EventScopeFactory<Official> eventScopeFactory)
        : base("officials", client, eventScopeFactory) { }
}
