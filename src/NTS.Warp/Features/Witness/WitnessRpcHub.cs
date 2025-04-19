using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;
using Not.Concurrency.Extensions;
using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Factories;
using NTS.Warp.ACL.RPC.Procedures;
using NTS.Warp.Features.Judge;
using NTS.Warp.Features.Witness.ProcessSnapshots;

namespace NTS.Warp.Features.Witness;

internal class WitnessRpcHub
    : NtsHub<ILegacyWitnessClientProcedures>,
        IEmsStartlistHubProcedures,
        IEmsParticipantsHubProcedures
{
    readonly IPrimaryConnectionContext _primaryConnections;
    readonly IHubContext<JudgeRpcHub, IJudgeClientProcedures> _judgeRelay;

    public WitnessRpcHub(
        IPrimaryConnectionContext primaryConnections,
        IHubContext<JudgeRpcHub, IJudgeClientProcedures> judgeRelay
    )
    {
        _primaryConnections = primaryConnections;
        _judgeRelay = judgeRelay;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var enduranceEventId = Context.GetHttpContext()!.Request.Query[WarpConstants.EVENT_GROUP_ID_KEY].ToString();
        if (!TryGetJudgeClient(enduranceEventId, out var judgeClient))
        {
            return;
        }
        await judgeClient.OnWitnessConnected(Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        var enduranceEventId = Context.GetHttpContext()!.Request.Query[WarpConstants.EVENT_GROUP_ID_KEY].ToString();
        if (!TryGetJudgeClient(enduranceEventId, out var judgeClient))
        {
            return;
        }
        await judgeClient.OnWitnessDisconnedted(Context.ConnectionId);
    }

    public Dictionary<int, EmsStartlist> SendStartlist(WarpRequest request)
    {
        if (!TryGetJudgeClient(request.EnduranceEventId, out var judgeClient))
        {
            return [];
        }
        var participations = judgeClient.GetActiveParticipations().Result;
        return StartlistFactory.Create(participations);
    }

    public async Task<EmsParticipantsPayload> SendParticipants(WarpRequest request)
    {
        if (!TryGetJudgeClient(request.EnduranceEventId, out var judgeClient))
        {
            return new EmsParticipantsPayload();
        }
        var participants = await judgeClient.GetActiveParticipations().Select(ParticipantEntryFactory.Create);
        return new EmsParticipantsPayload { Participants = participants.ToList(), EventId = int.Parse(request.EnduranceEventId) };
    }

    public async Task ReceiveWitnessEvent(WarpRequest<ProcessSnapshotsPayload> request)
    {
        if (!TryGetJudgeClient(request.EnduranceEventId, out var judgeClient))
        {
            return; // TODO: meaningful message would improve UX here
        }
        var snapshots = request.Payload.Entries.Select(entry => SnapshotFactory.Create(entry, request.Payload.Type));
        await judgeClient.ProcessSnapshots(snapshots);        
    }

    bool TryGetJudgeClient(string enduranceEventId, [NotNullWhen(true)] out IJudgeClientProcedures? judeClient)
    {
        var identifier = enduranceEventId.ToString();
        var connectionId = _primaryConnections.GetConnectionId(identifier);
        if (connectionId == null)
        {
            judeClient = null;
            return false;
        }

        judeClient = _judgeRelay.Clients.Client(connectionId);
        return true;
    }
}
