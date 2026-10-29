using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

/// <summary>
/// The Participations as the Api serves them (#604, ADR-0006, ADR-0013): the <c>participations</c> resource, which counts its
/// writes. What a list reads shows the version of each Participation, and a change is made against it: the Api refuses one
/// that was made on a version that has changed since, with the code <c>participation-changed</c> in
/// <see cref="Not.Storage.REST.JsonApiRepository{T, TModel}.LastError"/>, and the Ui reads the Participation again.
/// </summary>
public class ParticipationApiRepository : EventDataApiRepository<Participation, ParticipationModel>
{
    protected ParticipationApiRepository(JsonApiClient client, IRepositoryScopeFactory<Participation> scopeFactory)
        : base("participations", client, scopeFactory) { }

    public ParticipationApiRepository(JsonApiClient client)
        : base("participations", client, null) { }
}
