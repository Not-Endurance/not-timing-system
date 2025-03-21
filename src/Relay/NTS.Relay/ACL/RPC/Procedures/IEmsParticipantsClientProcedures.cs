using NTS.Relay.ACL.Entities;
using NTS.Relay.ACL.Enums;

namespace NTS.Relay.ACL.RPC.Procedures;

public interface IEmsParticipantsClientProcedures
{
    Task ReceiveEntryUpdate(EmsParticipantEntry entry, EmsCollectionAction action);
}
