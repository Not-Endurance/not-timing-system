using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.PastEvents;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class PastOfficialRepository
    : RestApiRepository<Official, OfficialModel>,
        IPastOfficialRepository
{
    public PastOfficialRepository(NHttpClient client)
        : base("officials", client) { }

    public async Task<IEnumerable<Official>> ReadForEvent(int eventId)
    {
        var models = await HandleRequest(Client.Get<IEnumerable<OfficialModel>>($"events/{eventId}/{Endpoint}")) ?? [];
        return models.Select(x => MapEntity(x)!);
    }
}
