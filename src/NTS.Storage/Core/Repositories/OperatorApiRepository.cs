using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class OperatorApiRepository : ApiRepository<Operator, OperatorModel>
{
    public OperatorApiRepository(NHttpClient client)
        : base("operators", client) { }
}
