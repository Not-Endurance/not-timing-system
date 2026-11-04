using NC.Application.HTTP;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Storage.REST;

namespace NoTiming.Storage.Core.Repositories;

public class ParticipationEventScopedApiRepository : EventScopedApiRepository<Participation, ParticipationModel>
{
    public ParticipationEventScopedApiRepository(NHttpClient client, EventScopeFactory<Participation> eventScopeFactory)
        : base("participations", client, eventScopeFactory) { }
}
