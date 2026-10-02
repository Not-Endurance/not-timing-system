using Not.Application.HTTP;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NoTiming.Ui.Storage.REST;

namespace NoTiming.Ui.Storage.Core.Repositories;

public class OperatorEventScopedApiRepository : EventScopedApiRepository<Operator, OperatorModel>
{
    public OperatorEventScopedApiRepository(NHttpClient client, EventScopeFactory<Operator> eventScopeFactory)
        : base("operators", client, eventScopeFactory) { }
}
