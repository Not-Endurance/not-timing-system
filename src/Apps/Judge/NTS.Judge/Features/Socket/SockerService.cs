using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;

namespace NTS.Judge.Features.Socket;

public class SockerService : ISocketService, ISocketConnectionsRegistry, ISingleton, IDisposable
{
    readonly IRpcSocket _rpcSocket;
    readonly HashSet<string> _connections = [];

    public SockerService(IRpcSocket rpcSocket)
    {
        _rpcSocket = rpcSocket;
        _rpcSocket.Error += HandleRpcErrors;
        _rpcSocket.ServerConnectionChanged += HandleServerConnectionChanged;
    }

    public SocketConnectionStatus Status { get; private set; }
    public bool IsConnected => _rpcSocket?.IsConnected ?? false;
    public IEnumerable<string> RemoteConnections => _connections;

    public void Add(string connectionId)
    {
        _connections.Add(connectionId);
    }

    public void Remove(string connectionId)
    {
        _connections.Remove(connectionId);
    }

    public void Dispose()
    {
        _rpcSocket.Error -= HandleRpcErrors;
        _rpcSocket.ServerConnectionChanged -= HandleServerConnectionChanged;
    }

    void HandleRpcErrors(object? sender, RpcError rpcError)
    {
        Status = SocketConnectionStatus.Disconnected;
        NotifyHelper.Error(rpcError.Exception);
    }

    void HandleServerConnectionChanged(object? sender, SocketConnectionStatus e)
    {
        Status = e;
    }
}
