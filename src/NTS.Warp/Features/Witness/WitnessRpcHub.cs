using Microsoft.AspNetCore.SignalR;
using Not.Application.CRUD.Ports;
using Not.Concurrency.Extensions;
using Not.Safe;
using NTS.Domain.Core.Aggregates;
using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;
using NTS.Warp.ACL.Factories;
using NTS.Warp.ACL.RPC.Procedures;
using NTS.Warp.Features.Judge;

namespace NTS.Warp.Features.Witness;

internal class WitnessRpcHub
    : Hub<ILegacyWitnessClientProcedures>,
        IEmsStartlistHubProcedures,
        IEmsParticipantsHubProcedures
{
    readonly IJudgeConnectionContext _judgeConnectionContext;
    readonly IHubContext<JudgeRpcHub, IJudgeRemoteProcedures> _judgeRelay;

    public WitnessRpcHub(
        IJudgeConnectionContext judgeConnectionContext,
        IHubContext<JudgeRpcHub, IJudgeRemoteProcedures> judgeRelay
    )
    {
        _judgeConnectionContext = judgeConnectionContext;
        _judgeRelay = judgeRelay;
    }

    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        await _judgeRelay.Clients.All.ReceiveRemoteConnectionId(connectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
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
        var enduranceEventId = await judge.GetEventId();
        return new EmsParticipantsPayload { Participants = participants.ToList(), EventId = enduranceEventId ?? 0 };
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
