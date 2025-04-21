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

internal class JudgeRpcHub : NtsHub<IJudgeClientProcedures>, IJudgeHubProcedures
{
    readonly IHubContext<WitnessRpcHub, ILegacyWitnessClientProcedures> _witnessRelay;
    readonly PrimaryConnectionsContext _primaryConnections;

    public JudgeRpcHub(
        ILogger<JudgeRpcHub> logger,
        IHubContext<WitnessRpcHub, ILegacyWitnessClientProcedures> witnessRelay,
        PrimaryConnectionsContext primaryConnections
    )
        : base(logger)
    {
        _witnessRelay = witnessRelay;
        _primaryConnections = primaryConnections;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var enduranceEventId = GetConnectionGroup()!;
        _primaryConnections.Add(enduranceEventId, Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        _primaryConnections.Remove(Context.ConnectionId);
    }

    public async Task OnParticipationEliminated(WarpRequest<ParticipationEliminated> request)
    {
        var emsParticipation = Convert(request.Payload.Participation);
        var entry = new EmsParticipantEntry(emsParticipation);
        await _witnessRelay
            .Clients.Group(request.EnduranceEventId)
            .ReceiveEntryUpdate(entry, EmsCollectionAction.Remove);
    }

    public async Task OnParticipationRestored(WarpRequest<ParticipationRestored> request)
    {
        var emsParticipation = Convert(request.Payload.Participation);
        var entry = new EmsParticipantEntry(emsParticipation);
        await _witnessRelay
            .Clients.Group(request.EnduranceEventId)
            .ReceiveEntryUpdate(entry, EmsCollectionAction.AddOrUpdate);
    }

    public async Task OnPhaseCompleted(WarpRequest<PhaseCompleted> request)
    {
        var emsParticipation = Convert(request.Payload.Participation);
        var entry = new EmsStartlistEntry(emsParticipation);
        await _witnessRelay
            .Clients.Group(request.EnduranceEventId)
            .ReceiveEntry(entry, EmsCollectionAction.AddOrUpdate);
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
