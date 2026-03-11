using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class RankingRepository : RestApiRepository<Ranking, RankingModel>, ITransient
{
    public RankingRepository(NHttpClient client)
        : base("rankings", client) { }
}
