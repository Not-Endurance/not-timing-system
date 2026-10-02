using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

public class ParticipationEventScopedApiRepository : EventScopedApiRepository<Participation, ParticipationModel>
{
    public ParticipationEventScopedApiRepository(NHttpClient client, EventScopeFactory<Participation> eventScopeFactory)
        : base("participations", client, eventScopeFactory) { }
}
