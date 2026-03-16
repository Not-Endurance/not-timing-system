using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class RankingRepository : EventScopedApiRepository<Ranking, RankingModel>, ITransient
{
    public RankingRepository(NHttpClient client, INtsSocketContext socketContext)
        : base("rankings", client, socketContext) { }
}
