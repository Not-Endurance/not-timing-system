using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

/// <summary>The Participations of the Event the Ui has selected: every list names it.</summary>
public class ParticipationEventScopedApiRepository : ParticipationApiRepository, IEventScopedRepository<Participation>
{
    public ParticipationEventScopedApiRepository(
        JsonApiClient client,
        EventScopeFactory<Participation> eventScopeFactory
    )
        : base(client, eventScopeFactory) { }
}
