using NTS.Relay.ACL.Entities;
using NTS.Relay.ACL.Entities.EMS;

namespace NTS.Relay.ACL.RPC.Procedures;

public interface IEmsParticipantsHubProcedures
{
    Task<EmsParticipantsPayload> SendParticipants();
    Task ReceiveWitnessEvent(IEnumerable<EmsParticipantEntry> entries, EmsWitnessEventType type);
}
