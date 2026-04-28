using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.PastEvents;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class PastRankingRepository
    : RestApiRepository<Ranking, RankingModel>,
        IPastRankingRepository
{
    public PastRankingRepository(NHttpClient client)
        : base("rankings", client) { }

    public async Task<IEnumerable<Ranking>> ReadForEvent(int eventId)
    {
        var models = await HandleRequest(Client.Get<IEnumerable<RankingModel>>($"events/{eventId}/{Endpoint}")) ?? [];
        return models.Select(x => MapEntity(x)!);
    }
}
