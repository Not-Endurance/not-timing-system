using Not.Application.RPC.Clients;
using NTS.Application.Models;

namespace NTS.Warp.Features.Witness.Procedures;

public interface IWitnessHubProcedures
{
    Task<IEnumerable<CoreParticipationModel>> SendParticipations(WarpRequest request);
    Task<RpcInvokeResult> Receive(WarpRequest<SnapshotModel> request);
}
