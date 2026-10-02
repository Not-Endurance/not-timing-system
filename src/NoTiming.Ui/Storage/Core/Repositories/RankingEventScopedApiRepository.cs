using Not.Application.HTTP;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NoTiming.Ui.Storage.REST;

namespace NoTiming.Ui.Storage.Core.Repositories;

public class RankingEventScopedApiRepository : EventScopedApiRepository<Ranking, RankingModel>
{
    public RankingEventScopedApiRepository(NHttpClient client, EventScopeFactory<Ranking> eventScopeFactory)
        : base("rankings", client, eventScopeFactory) { }
}
