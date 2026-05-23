using Not.Application.HTTP;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class OperatorEventScopedApiRepository : EventScopedApiRepository<Operator, OperatorModel>
{
    public OperatorEventScopedApiRepository(NHttpClient client, EventScopeFactory<Operator> eventScopeFactory)
        : base("operators", client, eventScopeFactory) { }
}
