using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class RankingRestApiRepository : ApiRepository<Ranking, RankingModel>, ITransient
{
    public RankingRestApiRepository(NHttpClient client)
        : base("rankings", client) { }
}
