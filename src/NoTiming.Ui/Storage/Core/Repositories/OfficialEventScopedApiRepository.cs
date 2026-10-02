using Not.Application.HTTP;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NoTiming.Ui.Storage.REST;

namespace NoTiming.Ui.Storage.Core.Repositories;

public class OfficialEventScopedApiRepository : EventScopedApiRepository<Official, OfficialModel>
{
    public OfficialEventScopedApiRepository(NHttpClient client, EventScopeFactory<Official> eventScopeFactory)
        : base("officials", client, eventScopeFactory) { }
}
