using Not.Application.HTTP;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NoTiming.Ui.Storage.REST;

namespace NoTiming.Ui.Storage.Core.Repositories;

public class ParticipationEventScopedApiRepository : EventScopedApiRepository<Participation, ParticipationModel>
{
    public ParticipationEventScopedApiRepository(NHttpClient client, EventScopeFactory<Participation> eventScopeFactory)
        : base("participations", client, eventScopeFactory) { }
}
