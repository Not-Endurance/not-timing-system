using Not.Application.RPC.SignalR;

namespace Not.Application.RPC;

public interface ISocketContext
{
    bool IsConnected { get; }
    SocketConnectionStatus Status { get; }
}
