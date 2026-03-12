using NTS.Application.Watcher;

namespace NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;

public interface IWitnessHubProcedures
{
    Task Receive(WarpRequest<SnapshotModel> request);
}
