using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class ParticipationApiRepository : ApiRepository<Participation, ParticipationModel>
{
    public ParticipationApiRepository(NHttpClient client)
        : base("participations", client) { }
}
