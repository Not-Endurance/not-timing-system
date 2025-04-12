using Microsoft.AspNetCore.SignalR;
using NTS.Application.RPC;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;
using NTS.Warp.ACL.Factories;
using NTS.Warp.RPC.Procedures;

namespace NTS.Warp.RPC;

internal class JudgeRpcHub : Hub<IJudgeClientProcedures>, IJudgeHubProcedures
{
    readonly IHubContext<WitnessRpcHub, ILegacyWitnessClientProcedures> _witnessRelay;

    public JudgeRpcHub(IHubContext<WitnessRpcHub, ILegacyWitnessClientProcedures> witnessRelay)
    {
        _witnessRelay = witnessRelay;
    }

    public async Task SendParticipationEliminated(ParticipationEliminated revoked)
    {
        var emsParticipation = ParticipationFactory.CreateEms(revoked.Participation);
        var entry = new EmsParticipantEntry(emsParticipation);
        await _witnessRelay.Clients.All.ReceiveEntryUpdate(entry, EmsCollectionAction.Remove);
    }

    public async Task SendParticipationRestored(ParticipationRestored restored)
    {
        var emsParticipation = ParticipationFactory.CreateEms(restored.Participation);
        var entry = new EmsParticipantEntry(emsParticipation);
        await _witnessRelay.Clients.All.ReceiveEntryUpdate(entry, EmsCollectionAction.AddOrUpdate);
    }

    public async Task SendStartCreated(PhaseCompleted phaseCompleted)
    {
        var emsParticipation = ParticipationFactory.CreateEms(phaseCompleted.Participation);
        var entry = new EmsStartlistEntry(emsParticipation);
        await _witnessRelay.Clients.All.ReceiveEntry(entry, EmsCollectionAction.AddOrUpdate);
    }
}
