using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

public class RankingEventScopedApiRepository : EventScopedApiRepository<Ranking, RankingModel>
{
    public RankingEventScopedApiRepository(NHttpClient client, EventScopeFactory<Ranking> eventScopeFactory)
        : base("rankings", client, eventScopeFactory) { }
}
