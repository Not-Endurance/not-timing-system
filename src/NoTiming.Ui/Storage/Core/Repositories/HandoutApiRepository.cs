using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

/// <summary>The Handouts of an Event as the Api serves them (#604, ADR-0006): the <c>handouts</c> resource, found by the Participation they are of.</summary>
public class HandoutApiRepository : EventDataApiRepository<Handout, HandoutModel>
{
    protected HandoutApiRepository(JsonApiClient client, IRepositoryScopeFactory<Handout> scopeFactory)
        : base("handouts", client, scopeFactory) { }

    public HandoutApiRepository(JsonApiClient client)
        : base("handouts", client, null) { }
}
