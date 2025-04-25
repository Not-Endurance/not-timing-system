using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;
using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;

namespace NTS.Tools.Watcher;

public class WatcherClient : RpcClient
{
    private readonly SignalRSocket _socket;

    public static async Task<WatcherClient> Create()
    {
        var socket = new SignalRSocket();
        //await socket.Connect("localhost");
        return new WatcherClient(socket);
    }

    private WatcherClient(SignalRSocket socket)
        : base(socket)
    {
        _socket = socket;
    }

    public async Task Send(ParticipantEntry entry, WitnessEventType type)
    {
        await _socket.Connect("localhost");

        var entries = new List<ParticipantEntry> { entry };
        var result = await InvokeHubProcedure(nameof(IParticipantstHubProcedures.ReceiveWitnessEvent), entries, type);

        await _socket.Disconnect();
    }
}
