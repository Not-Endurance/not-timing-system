using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.PastEvents;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class PastParticipationRepository
    : RestApiRepository<Participation, ParticipationModel>,
        IPastParticipationRepository
{
    public PastParticipationRepository(NHttpClient client)
        : base("participations", client) { }

    public async Task<IEnumerable<Participation>> ReadForEvent(int eventId)
    {
        var models =
            await HandleRequest(Client.Get<IEnumerable<ParticipationModel>>($"events/{eventId}/{Endpoint}")) ?? [];
        return models.Select(x => MapEntity(x)!);
    }
}
