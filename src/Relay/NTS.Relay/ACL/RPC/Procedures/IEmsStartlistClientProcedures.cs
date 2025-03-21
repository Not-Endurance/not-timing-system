using NTS.Relay.ACL.Enums;

namespace NTS.Relay.ACL.RPC.Procedures;

public interface IEmsStartlistClientProcedures
{
    Task ReceiveEntry(EmsStartlistEntry entry, EmsCollectionAction action);
}
