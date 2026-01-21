using Not.Application.RPC.Clients;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Services;

public interface IClientParticipationGetter
{
    Task<RpcInvokeResult<IEnumerable<Participation>>> GetParticipations();
}
