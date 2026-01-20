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

    public async Task<IEnumerable<Participation>> ReadMany()
    {
        var response = await _client.GetParticipations();
        if (response.Data == null)
        {
            return [];
        }
        return response.Data;
    }

    public Task<IEnumerable<Participation>> ReadMany(Expression<Func<Participation, bool>> filter)
    {
        throw new NotImplementedException();
    }
}
