using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;

namespace NTS.Warp.ACL.RPC.Procedures;

public interface IEmsParticipantsClientProcedures
{
    Task ReceiveEntryUpdate(EmsParticipantEntry entry, EmsCollectionAction action);
}
