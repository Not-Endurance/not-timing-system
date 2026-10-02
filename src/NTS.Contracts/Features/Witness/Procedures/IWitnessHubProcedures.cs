using NTS.Contracts.Watcher;
using NTS.Contracts.Watcher.Models;

namespace NTS.Contracts.Features.Witness.Procedures;

public interface IWitnessHubProcedures
{
    Task Receive(WarpRequest<SnapshotGroupModel> request);
}
