using Not.Application.HTTP;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class RankingEventScopedApiRepository : EventScopedApiRepository<Ranking, RankingModel>
{
    public RankingEventScopedApiRepository(NHttpClient client, EventScopeFactory<Ranking> eventScopeFactory)
        : base("rankings", client, eventScopeFactory) { }
}
