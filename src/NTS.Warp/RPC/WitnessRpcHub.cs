using Microsoft.AspNetCore.SignalR;
using Not.Application.CRUD.Ports;
using Not.Concurrency.Extensions;
using Not.Safe;
using NTS.Domain.Core.Aggregates;
using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;
using NTS.Warp.ACL.Factories;
using NTS.Warp.ACL.RPC.Procedures;
using NTS.Warp.Features;
using NTS.Warp.Features.Judge.ACL;

namespace NTS.Warp.RPC;

internal class WitnessRpcHub : Hub<ILegacyWitnessClientProcedures>, IEmsStartlistHubProcedures, IEmsParticipantsHubProcedures
{
    readonly IPrimaryConnectionContext _judgeConnectionContext;
    readonly IRead<EnduranceEvent> _events;
    readonly IHubContext<JudgeRpcHub, IJudgeRemoteProcedures> _judgeRelay;

    public WitnessRpcHub(
        IPrimaryConnectionContext judgeConnectionContext,
        IRead<EnduranceEvent> events,
        IHubContext<JudgeRpcHub, IJudgeRemoteProcedures> judgeRelay
    )
    {
        _judgeConnectionContext = judgeConnectionContext;
        _events = events;
        _judgeRelay = judgeRelay;
    }

    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        Console.WriteLine($"Connected: {Context.ConnectionId}");
        await _judgeRelay.Clients.All.ReceiveRemoteConnectionId(connectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        Console.WriteLine($"Disconnected: {Context.ConnectionId}");
        await _judgeRelay.Clients.All.ReceiveRemoteDisconnectId(connectionId);
    }

    public Dictionary<int, EmsStartlist> SendStartlist()
    {
        if (_judgeConnectionContext.Id == null)
        {
            return [];
        }
        var judge = _judgeRelay.Clients.Client(_judgeConnectionContext.Id);
        var participations = judge.GetActiveParticipations().Result;
        return StartlistFactory.Create(participations);
    }

    public async Task<EmsParticipantsPayload> SendParticipants()
    {
        if (_judgeConnectionContext.Id == null)
        {
            return new EmsParticipantsPayload();
        }
        var judge = _judgeRelay.Clients.Client(_judgeConnectionContext.Id);
        var participants = await judge.GetActiveParticipations().Select(ParticipantEntryFactory.Create);
        var enduranceEvent = await _events.Read(0);
        return new EmsParticipantsPayload { Participants = participants.ToList(), EventId = enduranceEvent?.Id ?? 0 };
    }

    public async Task ReceiveWitnessEvent(IEnumerable<EmsParticipantEntry> entries, EmsWitnessEventType type)
    {
        // Task.Run because Event hadling in dotnet seems to hold the current thread. Further investigation is needed
        // but what was happening is that Witness apps didn't receive rpc response untill the handling thread was finished
        // which is motly visible when it causes a validation (popup) which blocks the thread until closed in Prism/WPF
        await SafeHelper.RunAsync(async () =>
        {
            var snapshots = entries.Select(entry => SnapshotFactory.Create(entry, type));
            await _judgeRelay.Clients.All.ProcessSnapshots(snapshots);
        });
    }
}
