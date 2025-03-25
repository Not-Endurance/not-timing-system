using Not.Application.Behinds.Adapters;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Notify;

namespace NTS.Judge.Core.Behinds.Adapters;

public class ConnectionsBehind : ObservableBehind, IConnectionsBehind, IConnectionsRegistry, IDisposable
{
    readonly IRpcSocket _rpcSocket;
    HashSet<string> _connections = [];

    public ConnectionsBehind(IRpcSocket rpcSocket)
    {
        _rpcSocket = rpcSocket;
        IsServerConnected = _rpcSocket.IsConnected;
        _rpcSocket.Error += HandleRpcErrors;
        _rpcSocket.ServerConnectionChanged += HandleServerConnectionChanged;
    }

    public RpcConnectionStatus ServerConnectionStatus { get; private set; }
    public bool IsServerConnected { get; private set; }
    public IEnumerable<string> RemoteConnections => _connections;

    protected override Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        ServerConnectionStatus = RpcConnectionStatus.Disconnected;
        IsServerConnected = false;
        var result = _connections.Any();
        return Task.FromResult(result);
    }

    public void Add(string connectionId)
    {
        _connections.Add(connectionId);
        EmitChange();
    }

    public void Remove(string connectionId)
    {
        _connections.Remove(connectionId);
        EmitChange();
    }

    public void Dispose()
    {
        _rpcSocket.Error -= HandleRpcErrors;
        _rpcSocket.ServerConnectionChanged -= HandleServerConnectionChanged;
    }

    void HandleRpcErrors(object? sender, RpcError rpcError)
    {
        ServerConnectionStatus = RpcConnectionStatus.Disconnected;
        IsServerConnected = ServerConnectionStatus.Equals(RpcConnectionStatus.Connected);
        NotifyHelper.Error(rpcError.Exception);
        EmitChange();
    }

    void HandleServerConnectionChanged(object? sender, RpcConnectionStatus e)
    {
        ServerConnectionStatus = e;
        IsServerConnected = ServerConnectionStatus.Equals(RpcConnectionStatus.Connected);
        EmitChange();
    }
}
