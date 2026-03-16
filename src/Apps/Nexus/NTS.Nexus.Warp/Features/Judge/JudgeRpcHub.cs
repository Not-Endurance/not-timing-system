using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Nexus.Warp.Contracts;
using NTS.Nexus.Warp.Contracts.Features.Judge.Procedures;
using NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;
using NTS.Nexus.Warp.Features.Witness;

namespace NTS.Nexus.Warp.Features.Judge;

internal class JudgeRpcHub : NtsHub<IJudgeClientProcedures>, IJudgeHubProcedures
{
    readonly ILogger<JudgeRpcHub> _logger;
    readonly IHubContext<WitnessRpcHub, IWitnessClientProcedures> _witnessRelay;
    readonly PrimaryConnectionsContext _primaryConnections;
    readonly IPendingSnapshotsService _pendingSnapshots;

    public JudgeRpcHub(
        ILogger<JudgeRpcHub> logger,
        IHubContext<WitnessRpcHub, IWitnessClientProcedures> witnessRelay,
        PrimaryConnectionsContext primaryConnections,
        IPendingSnapshotsService pendingSnapshots
    )
        : base(logger)
    {
        _logger = logger;
        _witnessRelay = witnessRelay;
        _primaryConnections = primaryConnections;
        _pendingSnapshots = pendingSnapshots;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var enduranceEventId = GetConnectionGroup()!;
        _primaryConnections.Add(enduranceEventId, Context.ConnectionId);
        await FlushPendingSnapshots(enduranceEventId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        _primaryConnections.Remove(Context.ConnectionId);
    }

    public async Task OnParticipationEliminated(WarpRequest<ParticipationEliminated> request)
    {
        await _witnessRelay.Clients.Group(request.EnduranceEventId).OnParticipationEliminated(request.Payload);
    }

    public async Task OnParticipationRestored(WarpRequest<ParticipationRestored> request)
    {
        await _witnessRelay.Clients.Group(request.EnduranceEventId).OnParticipationRestored(request.Payload);
    }

    public async Task OnPhaseCompleted(WarpRequest<PhaseCompleted> request)
    {
        var participation = request.Payload.Participation;
        _logger.LogInformation(
            "Phase completed IN: #{number}, OUT: {outTime}",
            participation.Combination.Number,
            participation.Phases.Last(x => x.StartTime != null).StartTime
        );

        await _witnessRelay.Clients.Group(request.EnduranceEventId).OnPhaseCompleted(request.Payload);

        var serialized = JsonConvert.SerializeObject(participation);
        _logger.LogInformation(
            "Phase completed OUT: #{number}, OUT: {outTime}, serialized: {serialized}",
            participation.Combination.Number,
            participation.Phases.Current.StartTime,
            serialized
        );
    }

    async Task FlushPendingSnapshots(string enduranceEventId)
    {
        var pendingDocuments = await _pendingSnapshots.Read(enduranceEventId);
        if (pendingDocuments.Count == 0)
        {
            return;
        }

        var pendingSnapshotCount = pendingDocuments.Sum(x => x.SnapshotGroups.Length);
        try
        {
            foreach (var pendingDocument in pendingDocuments)
            {
                foreach (var pendingSnapshotGroup in pendingDocument.SnapshotGroups)
                {
                    await Clients.Caller.Receive(pendingSnapshotGroup);
                }
                await _pendingSnapshots.Remove(pendingDocument);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to flush {count} pending witness snapshots for event '{enduranceEventId}'. Keeping them persisted.",
                pendingSnapshotCount,
                enduranceEventId
            );
        }
    }
}
