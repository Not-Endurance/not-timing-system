using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;
using NTS.Warp.Features.Witness.ProcessSnapshots;

namespace NTS.Warp.ACL.RPC.Procedures;

public interface IEmsParticipantsHubProcedures
{
    Task<IEnumerable<EmsParticipantEntry>> SendParticipants(WarpRequest request);
    Task ReceiveWitnessEvent(WarpRequest<ProcessSnapshotsPayload> request);
}
