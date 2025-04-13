using Microsoft.AspNetCore.SignalR;
using NTS.Application.RPC;
using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;
using NTS.Warp.ACL.Factories;
using NTS.Warp.Features;
using NTS.Warp.Features.Judge.ACL;
using NTS.Warp.Features.Judge.Models;
using NTS.Warp.Features.Judge.Procedures;

namespace NTS.Warp.RPC;

internal class JudgeRpcHub : Hub<IJudgeRemoteProcedures>, IJudgeHubProcedures
{
    readonly IHubContext<WitnessRpcHub, ILegacyWitnessClientProcedures> _witnessRelay;
    readonly PrimaryConnectionContext _primaryConnectionContext;

    public JudgeRpcHub(IHubContext<WitnessRpcHub, ILegacyWitnessClientProcedures> witnessRelay, PrimaryConnectionContext primaryConnectionContext)
    {
        _witnessRelay = witnessRelay;
        _primaryConnectionContext = primaryConnectionContext;
    }

    public override Task OnConnectedAsync()
    {
        _primaryConnectionContext.Id = Context.ConnectionId;
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _primaryConnectionContext.Id = null;
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

public interface IJudgeRemoteProcedures : IParticipationRemoteProcedures, IConnectionsClientProcedures
{
}
