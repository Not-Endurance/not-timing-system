using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

/// <summary>The Handouts of the Event the Ui has selected: every list names it.</summary>
public class HandoutEventScopedApiRepository : HandoutApiRepository, IEventScopedRepository<Handout>
{
    public HandoutEventScopedApiRepository(JsonApiClient client, EventScopeFactory<Handout> eventScopeFactory)
        : base(client, eventScopeFactory) { }
}
