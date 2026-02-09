using Not.Application.RPC.SignalR;
using Not.Injection;

namespace Not.Application.RPC;

public interface ISocketService
{
    bool IsConnected { get; }
    IEnumerable<string> RemoteConnections { get; }
    SocketConnectionStatus Status { get; }
}
