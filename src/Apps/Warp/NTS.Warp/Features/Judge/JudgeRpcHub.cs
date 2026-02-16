using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using NTS.Domain.Core.Objects.Payloads;
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
        await _witnessRelay.Clients.Group(request.EnduranceEventId).ReceiveParticipation(participation);
    }

    public async Task OnParticipationRestored(WarpRequest<ParticipationRestored> request)
    {
        var participation = request.Payload.Participation;
        await _witnessRelay.Clients.Group(request.EnduranceEventId).ReceiveParticipation(participation);
    }

    public async Task OnPhaseCompleted(WarpRequest<PhaseCompleted> request)
    {
        var participation = request.Payload.Participation;
        _logger.LogInformation(
            "Phase completed IN: #{number}, OUT: {outTime}",
            participation.Combination.Number,
            participation.Phases.Last(x => x.StartTime != null).StartTime
        );

        await _witnessRelay.Clients.Group(request.EnduranceEventId).ReceiveParticipation(participation);

        var serialized = JsonConvert.SerializeObject(participation);
        _logger.LogInformation(
            "Phase completed OUT: #{number}, OUT: {outTime}, serialized: {serialized}",
            participation.Combination.Number,
            participation.Phases.Current.StartTime,
            serialized
        );
    }
}
