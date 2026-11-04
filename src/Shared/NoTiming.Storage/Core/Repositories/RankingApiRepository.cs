using NC.Application.HTTP;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Storage.Core.Repositories;

public class RankingApiRepository : ApiRepository<Ranking, RankingModel>
{
    public RankingApiRepository(NHttpClient client)
        : base("rankings", client) { }
}
