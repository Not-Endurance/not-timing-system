using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Notify;

namespace NTS.Judge.Features.Core.Behinds.Adapters;

public class ConnectionsBehind : IConnectionsBehind, IConnectionsRegistry, IDisposable
{
    readonly IRpcSocket _rpcSocket;
    readonly HashSet<string> _connections = [];

    public ConnectionsBehind(IRpcSocket rpcSocket)
    {
        _rpcSocket = rpcSocket;
        _rpcSocket.Error += HandleRpcErrors;
        _rpcSocket.ServerConnectionChanged += HandleServerConnectionChanged;
    }

    public RpcConnectionStatus ServerConnectionStatus { get; private set; }
    public bool IsServerConnected => _rpcSocket?.IsConnected ?? false;
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
        ServerConnectionStatus = RpcConnectionStatus.Disconnected;
        NotifyHelper.Error(rpcError.Exception);
    }

    void HandleServerConnectionChanged(object? sender, RpcConnectionStatus e)
    {
        ServerConnectionStatus = e;
    }
}
