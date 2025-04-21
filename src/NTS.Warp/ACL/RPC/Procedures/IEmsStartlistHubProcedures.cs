using NTS.Warp.ACL.Entities;

namespace NTS.Warp.ACL.RPC.Procedures;

public interface IEmsStartlistHubProcedures
{
    Task<Dictionary<int, EmsStartlist>> SendStartlist(WarpRequest request);
}
