using NC.Application.HTTP;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Storage.Core.Repositories;

public class OperatorApiRepository : ApiRepository<Operator, OperatorModel>
{
    public OperatorApiRepository(NHttpClient client)
        : base("operators", client) { }
}
