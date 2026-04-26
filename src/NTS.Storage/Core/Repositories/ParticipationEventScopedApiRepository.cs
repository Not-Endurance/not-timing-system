using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class ParticipationEventScopedApiRepository : EventScopedApiRepository<Participation, ParticipationModel>, ITransient
{
    public ParticipationEventScopedApiRepository(NHttpClient client, INtsSocketContext socketContext)
        : base("participations", client, socketContext) { }
}
