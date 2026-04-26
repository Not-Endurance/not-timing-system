using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class ParticipationRepository : EventScopedApiRepository<Participation, ParticipationModel>, ITransient
{
    public ParticipationRepository(NHttpClient client, INtsSocketContext socketContext)
        : base("participations", client, socketContext) { }
}
