using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

public class HandoutEventScopedApiRepository : EventScopedApiRepository<Handout, HandoutModel>
{
    public HandoutEventScopedApiRepository(NHttpClient client, EventScopeFactory<Handout> eventScopeFactory)
        : base("handouts", client, eventScopeFactory) { }
}
