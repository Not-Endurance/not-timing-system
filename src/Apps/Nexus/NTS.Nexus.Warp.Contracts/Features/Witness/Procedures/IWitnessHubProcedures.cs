using Not.Application.RPC.Clients;
using NTS.Application.Watcher;

namespace NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;

public interface IWitnessHubProcedures
{
    Task<RpcInvokeResult> Receive(WarpRequest<SnapshotModel> request);
}
