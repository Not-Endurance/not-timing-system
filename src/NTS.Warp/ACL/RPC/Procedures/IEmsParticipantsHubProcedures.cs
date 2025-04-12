using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;

namespace NTS.Warp.ACL.RPC.Procedures;

public interface IEmsParticipantsHubProcedures
{
    Task<EmsParticipantsPayload> SendParticipants();
    Task ReceiveWitnessEvent(IEnumerable<EmsParticipantEntry> entries, EmsWitnessEventType type);
}
