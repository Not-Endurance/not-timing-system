using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class ParticipationRepository : RestApiRepository<Participation, ParticipationModel>, ITransient
{
    public ParticipationRepository(NHttpClient client)
        : base("participations", client) { }
}
