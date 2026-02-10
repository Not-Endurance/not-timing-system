using Not.Application.RPC.SignalR;

namespace Not.Application.RPC;

public interface ISocketStatusContext
{
    bool IsConnected { get; }
    IEnumerable<string> RemoteConnections { get; }
    SocketConnectionStatus Status { get; }
}
