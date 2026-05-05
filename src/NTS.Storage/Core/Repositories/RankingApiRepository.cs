using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class RankingApiRepository : ApiRepository<Ranking, RankingModel>
{
    public RankingApiRepository(NHttpClient client)
        : base("rankings", client) { }
}
