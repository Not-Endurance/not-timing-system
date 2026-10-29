using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

/// <summary>The Officials of the Event the Ui has selected: every list names it.</summary>
public class OfficialEventScopedApiRepository : OfficialApiRepository, IEventScopedRepository<Official>
{
    public OfficialEventScopedApiRepository(JsonApiClient client, EventScopeFactory<Official> eventScopeFactory)
        : base(client, eventScopeFactory) { }
}
