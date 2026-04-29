using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;

namespace NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;

public interface IWitnessHubProcedures
{
    Task Receive(WarpRequest<SnapshotGroupModel> request);
}
