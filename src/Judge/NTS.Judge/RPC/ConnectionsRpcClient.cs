using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using NTS.Application.RPC;

namespace NTS.Judge.RPC;

public class ConnectionsRpcClient : RpcClient, IConnectionsClientProcedures
{
    readonly IConnectionsRegistry _connectionsRegistry;

    public ConnectionsRpcClient(IRpcSocket socket, IConnectionsRegistry connectionsRegistry)
        : base(socket)
    {
        _connectionsRegistry = connectionsRegistry;
    }

    public override void RunAtStartup()
    {
        RegisterClientProcedure<string>(nameof(ReceiveRemoteConnectionId), ReceiveRemoteConnectionId);
        RegisterClientProcedure<string>(nameof(ReceiveRemoteDisconnectId), ReceiveRemoteDisconnectId);
    }

    public Task ReceiveRemoteConnectionId(string connectionId)
    {
        _connectionsRegistry.Add(connectionId);
        return Task.CompletedTask;
    }

    public Task ReceiveRemoteDisconnectId(string connectionId)
    {
        _connectionsRegistry.Remove(connectionId);
        return Task.CompletedTask;
    }
}
