using Not.Application.HTTP;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class HandoutEventScopedApiRepository : EventScopedApiRepository<Handout, HandoutModel>
{
    public HandoutEventScopedApiRepository(NHttpClient client, EventScopeFactory<Handout> eventScopeFactory)
        : base("handouts", client, eventScopeFactory) { }
}
