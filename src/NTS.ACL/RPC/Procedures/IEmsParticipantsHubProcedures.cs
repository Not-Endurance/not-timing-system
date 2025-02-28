using NTS.ACL.Entities;
using NTS.ACL.Entities.EMS;

namespace NTS.ACL.RPC.Procedures;

public interface IEmsParticipantsHubProcedures
{
    Task<EmsParticipantsPayload> SendParticipants();
    Task ReceiveWitnessEvent(IEnumerable<EmsParticipantEntry> entries, EmsWitnessEventType type);
}
