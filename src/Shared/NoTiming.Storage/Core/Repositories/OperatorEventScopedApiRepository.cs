using NC.Application.HTTP;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Storage.REST;

namespace NoTiming.Storage.Core.Repositories;

public class OperatorEventScopedApiRepository : EventScopedApiRepository<Operator, OperatorModel>
{
    public OperatorEventScopedApiRepository(NHttpClient client, EventScopeFactory<Operator> eventScopeFactory)
        : base("operators", client, eventScopeFactory) { }
}
