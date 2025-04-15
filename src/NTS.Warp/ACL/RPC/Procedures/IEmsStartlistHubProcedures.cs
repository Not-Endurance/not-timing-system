using NTS.Warp.ACL.Entities;

namespace NTS.Warp.ACL.RPC.Procedures;

public interface IEmsStartlistHubProcedures
{
    Dictionary<int, EmsStartlist> SendStartlist();
}
