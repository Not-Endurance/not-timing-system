using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

/// <summary>The Rankings of the Event the Ui has selected: every list names it.</summary>
public class RankingEventScopedApiRepository : RankingApiRepository, IEventScopedRepository<Ranking>
{
    public RankingEventScopedApiRepository(JsonApiClient client, EventScopeFactory<Ranking> eventScopeFactory)
        : base(client, eventScopeFactory) { }
}
