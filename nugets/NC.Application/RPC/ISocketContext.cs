using System.Diagnostics.CodeAnalysis;
using NC.Application.RPC.SignalR;

namespace NC.Application.RPC;

public interface ISocketContext
{
    bool IsConnected { get; }
    SocketConnectionStatus Status { get; }
}
