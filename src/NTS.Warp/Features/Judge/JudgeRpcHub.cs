using Microsoft.AspNetCore.SignalR;
using NTS.Application.RPC;
using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;
using NTS.Warp.ACL.Factories;
using NTS.Warp.ACL.RPC.Procedures;
using NTS.Warp.Features.Judge.Models;
using NTS.Warp.Features.Judge.Procedures;
using NTS.Warp.Features.Witness;

namespace NTS.Warp.Features.Judge;

internal class JudgeRpcHub : Hub<IJudgeRemoteProcedures>, IJudgeHubProcedures
{
    readonly IHubContext<WitnessRpcHub, ILegacyWitnessClientProcedures> _witnessRelay;
    readonly JudgeConnectionContext _judgeConnectionContext;

    public JudgeRpcHub(IHubContext<WitnessRpcHub, ILegacyWitnessClientProcedures> witnessRelay, JudgeConnectionContext judgeConnectionContext)
    {
        _witnessRelay = witnessRelay;
        _judgeConnectionContext = judgeConnectionContext;
    }

    public override Task OnConnectedAsync()
    {
        _judgeConnectionContext.Id = Context.ConnectionId;
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _judgeConnectionContext.Id = null;
        return base.OnDisconnectedAsync(exception);
    }

    public async Task SendParticipationEliminated(ParticipationWarpDto participation)
    {
        var emsParticipation = ParticipationFactory.CreateEms(participation);
        var entry = new EmsParticipantEntry(emsParticipation);
        await _witnessRelay.Clients.All.ReceiveEntryUpdate(entry, EmsCollectionAction.Remove);
    }

    public async Task SendParticipationRestored(ParticipationWarpDto participation)
    {
        var emsParticipation = ParticipationFactory.CreateEms(participation);
        var entry = new EmsParticipantEntry(emsParticipation);
        await _witnessRelay.Clients.All.ReceiveEntryUpdate(entry, EmsCollectionAction.AddOrUpdate);
    }

    public async Task SendStartCreated(ParticipationWarpDto participation)
    {
        var emsParticipation = ParticipationFactory.CreateEms(participation);
        var entry = new EmsStartlistEntry(emsParticipation);
        await _witnessRelay.Clients.All.ReceiveEntry(entry, EmsCollectionAction.AddOrUpdate);
    }
}

public interface IJudgeRemoteProcedures : IParticipationRemoteProcedures, IConnectionsClientProcedures, IEnduranceEventRpcClient
{
}
