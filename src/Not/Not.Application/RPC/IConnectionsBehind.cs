using Not.Application.RPC.SignalR;
using Not.Injection;

namespace Not.Application.RPC;

public interface IConnectionsBehind : ISingleton
{
    IEnumerable<string> RemoteConnections { get; }
    bool IsServerConnected { get; }
    RpcConnectionStatus ServerConnectionStatus { get; }
}
