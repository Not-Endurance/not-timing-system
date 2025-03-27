using NTS.Relay.ACL.Entities;

namespace NTS.Relay.ACL.RPC.Procedures;

public interface IEmsStartlistHubProcedures
{
    Dictionary<int, EmsStartlist> SendStartlist();
}
