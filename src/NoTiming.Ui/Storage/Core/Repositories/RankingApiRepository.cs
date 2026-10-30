using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

/// <summary>The Rankings of an Event as the Api serves them (#604, ADR-0006): the <c>rankings</c> resource, which holds Participation ids.</summary>
public class RankingApiRepository : EventDataApiRepository<Ranking, RankingModel>
{
    protected RankingApiRepository(JsonApiClient client, IRepositoryScopeFactory<Ranking> scopeFactory)
        : base("rankings", client, scopeFactory) { }

    public RankingApiRepository(JsonApiClient client)
        : base("rankings", client, null) { }
}
