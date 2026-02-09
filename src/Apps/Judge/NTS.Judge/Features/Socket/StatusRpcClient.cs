using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Injection;
using NTS.Warp.Features.Judge.Procedures;

namespace NTS.Judge.Features.Socket;

public class StatusRpcClient : RpcClient, IStatusClientProcedures, ISingleton
{
    readonly ISocketConnectionsRegistry _connectionsRegistry;

    public StatusRpcClient(IRpcSocket socket, ISocketConnectionsRegistry connectionsRegistry)
        : base(socket)
    {
        _connectionsRegistry = connectionsRegistry;
    }

    public override void RunAtStartup()
    {
        RegisterInputProcedure<string>(nameof(OnWitnessConnected), OnWitnessConnected);
        RegisterInputProcedure<string>(nameof(OnWitnessDisconnected), OnWitnessDisconnected);
    }

    public Task OnWitnessConnected(string connectionId)
    {
        _connectionsRegistry.Add(connectionId);
        return Task.CompletedTask;
    }

    public Task OnWitnessDisconnected(string connectionId)
    {
        _connectionsRegistry.Remove(connectionId);
        return Task.CompletedTask;
    }
}
