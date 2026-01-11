using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Witness.RPC;

namespace NTS.Witness.Services;

public class ParticipationReader : IReadMany<Participation>, ITransient
{
    readonly WitnessRpcClient _client;

    public ParticipationReader(WitnessRpcClient client)
    {
        _client = client;
    }

    public Task<IEnumerable<Participation>> ReadMany()
    {
        return _client.GetParcipations();
    }

    public Task<IEnumerable<Participation>> ReadMany(Expression<Func<Participation, bool>> filter)
    {
        throw new NotImplementedException();
    }
}
