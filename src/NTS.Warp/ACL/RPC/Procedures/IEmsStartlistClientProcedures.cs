using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;

namespace NTS.Warp.ACL.RPC.Procedures;

public interface IEmsStartlistClientProcedures
{
    Task ReceiveEntry(EmsStartlistEntry entry, EmsCollectionAction action);
}
