using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Not.Collections;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Warp.Features.Judge.Procedures;
using NTS.Warp.Features.Witness;
using NTS.Warp.Features.Witness.Procedures;

namespace NTS.Warp.Features.Judge;

internal class JudgeRpcHub : NtsHub<IJudgeClientProcedures>, IJudgeHubProcedures
{
    readonly ILogger<JudgeRpcHub> _logger;
    readonly IHubContext<WitnessRpcHub, IWitnessClientProcedures> _witnessRelay;
    readonly PrimaryConnectionsContext _primaryConnections;

    public JudgeRpcHub(
        ILogger<JudgeRpcHub> logger,
        IHubContext<WitnessRpcHub, IWitnessClientProcedures> witnessRelay,
        PrimaryConnectionsContext primaryConnections
    )
        : base(logger)
    {
        _logger = logger;
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
        var participation = request.Payload.Participation;
        await _witnessRelay
            .Clients.Group(request.EnduranceEventId)
            .ReceiveParticipation(participation, NCollectionAction.Remove);
    }

    public async Task OnParticipationRestored(WarpRequest<ParticipationRestored> request)
    {
        var participation = request.Payload.Participation;
        await _witnessRelay
            .Clients.Group(request.EnduranceEventId)
            .ReceiveParticipation(participation, NCollectionAction.AddOrUpdate);

        if (request.Payload.Participation.Phases.Any(x => x.IsComplete()))
        {
            var startlistEntry = new StartlistEntry(request.Payload.Participation);
            await _witnessRelay
                .Clients.Group(request.EnduranceEventId)
                .ReceiveStartlistEntry(startlistEntry, NCollectionAction.AddOrUpdate);
        }
    }

    public async Task OnPhaseCompleted(WarpRequest<PhaseCompleted> request)
    {
        _logger.LogInformation(
            "Phase completed IN: #{number}, OUT: {outTime}",
            request.Payload.Participation.Combination.Number,
            request.Payload.Participation.Phases.Last(x => x.StartTime != null).StartTime
        );

        var entry = new StartlistEntry(request.Payload.Participation);

        var serialized = JsonConvert.SerializeObject(entry);
        _logger.LogInformation(
            "Phase completed OUT: #{number}, OUT: {outTime}, serialized: {serialized}",
            entry.Number,
            entry.Start,
            serialized
        );

        await _witnessRelay
            .Clients.Group(request.EnduranceEventId)
            .ReceiveStartlistEntry(entry, NCollectionAction.AddOrUpdate);
    }
}
