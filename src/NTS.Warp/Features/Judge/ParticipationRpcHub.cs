using Microsoft.AspNetCore.SignalR;
using NTS.Application.RPC;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Entities.Participations;
using NTS.Warp.ACL.Enums;
using NTS.Warp.ACL.Factories;
using NTS.Warp.ACL.RPC.Procedures;
using NTS.Warp.Features.Judge.Models;
using NTS.Warp.Features.Judge.Procedures;
using NTS.Warp.Features.Witness;

namespace NTS.Warp.Features.Judge;

internal class ParticipationRpcHub : Hub<IJudgeClientProcedures>, IJudgeHubProcedures
{
    readonly IHubContext<WitnessRpcHub, ILegacyWitnessClientProcedures> _witnessRelay;
    readonly JudgeConnectionContext _judgeConnectionContext;

    public ParticipationRpcHub(
        IHubContext<WitnessRpcHub, ILegacyWitnessClientProcedures> witnessRelay,
        JudgeConnectionContext judgeConnectionContext
    )
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

    public async Task OnParticipationEliminated(ParticipationEliminated eliminated)
    {
        var emsParticipation = Convert(eliminated.Participation);
        var entry = new EmsParticipantEntry(emsParticipation);
        await _witnessRelay.Clients.All.ReceiveEntryUpdate(entry, EmsCollectionAction.Remove);
    }

    public async Task OnParticipationRestored(ParticipationRestored restored)
    {
        var emsParticipation = Convert(restored.Participation);
        var entry = new EmsParticipantEntry(emsParticipation);
        await _witnessRelay.Clients.All.ReceiveEntryUpdate(entry, EmsCollectionAction.AddOrUpdate);
    }

    public async Task OnPhaseCompleted(PhaseCompleted phaseCompleted)
    {
        var emsParticipation = Convert(phaseCompleted.Participation);
        var entry = new EmsStartlistEntry(emsParticipation);
        await _witnessRelay.Clients.All.ReceiveEntry(entry, EmsCollectionAction.AddOrUpdate);
    }

    EmsParticipation Convert(Participation participation)
    {
        var dto = ParticipationWarpDto.Create(participation);
        return ParticipationFactory.CreateEms(dto);
    }
}

public interface IJudgeClientProcedures
    : IParticipationClientProcedures,
        IConnectionsClientProcedures,
        IEnduranceEventClientProcedures { }

public interface IJudgeHubProcedures : IParticipationHubProcedures { }
