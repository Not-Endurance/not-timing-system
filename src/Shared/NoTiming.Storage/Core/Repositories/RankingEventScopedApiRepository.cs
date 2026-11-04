using NC.Application.HTTP;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Storage.REST;

namespace NoTiming.Storage.Core.Repositories;

public class RankingEventScopedApiRepository : EventScopedApiRepository<Ranking, RankingModel>
{
    public RankingEventScopedApiRepository(NHttpClient client, EventScopeFactory<Ranking> eventScopeFactory)
        : base("rankings", client, eventScopeFactory) { }
}
