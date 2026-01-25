using Not.Application.RPC.Clients;
using NTS.Application.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Warp.Features.Witness.Procedures;

public interface IWitnessHubProcedures
{
    Task<IEnumerable<CoreParticipationModel>> SendParticipations(WarpRequest request);
    Task<IEnumerable<StartlistEntryModel>> SendStartlistEntries(WarpRequest request);
    Task<RpcInvokeResult> Receive(WarpRequest<SnapshotModel> request);
}
