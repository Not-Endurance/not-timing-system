using NC.Application.HTTP;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Storage.REST;

namespace NoTiming.Storage.Core.Repositories;

public class HandoutEventScopedApiRepository : EventScopedApiRepository<Handout, HandoutModel>
{
    public HandoutEventScopedApiRepository(NHttpClient client, EventScopeFactory<Handout> eventScopeFactory)
        : base("handouts", client, eventScopeFactory) { }
}
