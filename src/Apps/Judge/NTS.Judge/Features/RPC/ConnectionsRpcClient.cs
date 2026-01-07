using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using NTS.Warp.Features.Judge.Procedures;

namespace NTS.Judge.Features.RPC;

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
        RegisterInputProcedure<string>(nameof(OnWitnessConnected), OnWitnessConnected);
        RegisterInputProcedure<string>(nameof(OnWitnessDisconnedted), OnWitnessDisconnedted);
    }

    public Task OnWitnessConnected(string connectionId)
    {
        _connectionsRegistry.Add(connectionId);
        return Task.CompletedTask;
    }

    public Task OnWitnessDisconnedted(string connectionId)
    {
        _connectionsRegistry.Remove(connectionId);
        return Task.CompletedTask;
    }
}
