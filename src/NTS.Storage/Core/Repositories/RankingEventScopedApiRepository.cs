using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class RankingEventScopedApiRepository : EventScopedApiRepository<Ranking, RankingModel>, ITransient
{
    public RankingEventScopedApiRepository(NHttpClient client, EventScopeFactory<Ranking> eventScopeFactory)
        : base("rankings", client, eventScopeFactory) { }
}
