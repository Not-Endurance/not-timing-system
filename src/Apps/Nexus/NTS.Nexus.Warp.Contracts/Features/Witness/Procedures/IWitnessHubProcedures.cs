using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;

public interface IWitnessHubProcedures
{
    Task Receive(WarpRequest<SnapshotGroupModel> request);
    Task AcknoledgeVetIn(WarpRequest<VetInAcknoledged> request);
}
