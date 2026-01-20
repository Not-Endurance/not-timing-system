using Not.Application.RPC.Clients;
using NTS.Application.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Warp.Features.Witness.Procedures;

public interface IWitnessHubProcedures
{
    Task<IEnumerable<Participation>> SendParticipants(WarpRequest request);
    Task<Startlist?> SendStartlist(WarpRequest request);
    Task<RpcInvokeResult> Receive(WarpRequest<SnapshotModel> request);
}
