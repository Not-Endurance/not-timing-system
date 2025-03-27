using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using NTS.Application.RPC;

namespace NTS.Judge.RPC;

public class ConnectionsClient : RpcClient, IConnectionsRpcClient
{
    readonly IConnectionsRegistry _connectionsRegistry;

    public ConnectionsClient(IRpcSocket socket, IConnectionsRegistry connectionsRegistry)
        : base(socket)
    {
        _connectionsRegistry = connectionsRegistry;
        RegisterClientProcedure<string>(
            nameof(IJudgeClientProcedures.ReceiveRemoteConnectionId),
            ReceiveRemoteConnectionId
        );
        RegisterClientProcedure<string>(
            nameof(IJudgeClientProcedures.ReceiveRemoteDisconnectId),
            ReceiveRemoteDisconnectId
        );
    }

    public override void RunAtStartup()
    {
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

public interface IConnectionsRpcClient : IConnectionsClientProcedures, IRpcClient { }
