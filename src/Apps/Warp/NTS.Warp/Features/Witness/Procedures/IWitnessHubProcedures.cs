using Not.Application.RPC.Clients;
using NTS.Application.Core;
using NTS.Application.Watcher;

namespace NTS.Warp.Features.Witness.Procedures;

public interface IWitnessHubProcedures
{
    Task<IEnumerable<ParticipationModel>> SendParticipations(WarpRequest request);
    Task<RpcInvokeResult> Receive(WarpRequest<SnapshotModel> request);
}
