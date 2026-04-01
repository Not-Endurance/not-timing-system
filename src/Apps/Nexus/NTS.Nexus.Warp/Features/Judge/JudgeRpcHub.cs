using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Nexus.Warp.Abstractions;
using NTS.Nexus.Warp.ConnectionDiagnostics;
using NTS.Nexus.Warp.Contracts;
using NTS.Nexus.Warp.Contracts.Features.Judge.Procedures;
using NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;
using NTS.Nexus.Warp.Features.Witness;
using NTS.Nexus.Warp.Features.Witness.PendingSnapshots;

namespace NTS.Nexus.Warp.Features.Judge;

internal class JudgeRpcHub : NtsHub<IJudgeClientProcedures>, IJudgeHubProcedures
{
    readonly ILogger<JudgeRpcHub> _logger;
    readonly IHubContext<WitnessRpcHub, IWitnessClientProcedures> _witnessRelay;
    readonly JudgeConnectionsContext _primaryConnections;
    readonly IPendingSnapshotsService _pendingSnapshots;

    public JudgeRpcHub(
        ILogger<JudgeRpcHub> logger,
        IHubContext<WitnessRpcHub, IWitnessClientProcedures> witnessRelay,
        JudgeConnectionsContext primaryConnections,
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
        var stopwatch = Stopwatch.StartNew();
        await base.OnConnectedAsync();
        var enduranceEventId = GetConnectionGroup()!;
        _primaryConnections.Add(enduranceEventId, Context.ConnectionId);
        await FlushPendingSnapshots(enduranceEventId);
        stopwatch.Stop();
        _logger.LogInformation(
            "Judge hub OnConnectedAsync finished in {ElapsedMilliseconds} ms. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, EventId {EnduranceEventId}.",
            stopwatch.ElapsedMilliseconds,
            Context.ConnectionId,
            WarpConnectionDiagnostics.GetCorrelationId(Context.GetHttpContext()),
            enduranceEventId
        );
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
        var correlationId = WarpConnectionDiagnostics.GetCorrelationId(Context.GetHttpContext());
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Judge pending snapshot flush started. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, EventId {EnduranceEventId}.",
            Context.ConnectionId,
            correlationId,
            enduranceEventId
        );

        var pendingDocuments = await _pendingSnapshots.Read(enduranceEventId);
        var pendingSnapshotCount = pendingDocuments.Sum(x => x.SnapshotGroups.Length);
        if (pendingDocuments.Count == 0)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Judge pending snapshot flush completed in {ElapsedMilliseconds} ms with no persisted snapshots. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, EventId {EnduranceEventId}.",
                stopwatch.ElapsedMilliseconds,
                Context.ConnectionId,
                correlationId,
                enduranceEventId
            );
            return;
        }

        _logger.LogInformation(
            "Judge pending snapshot flush loaded {PendingDocumentCount} documents and {PendingSnapshotCount} snapshot groups. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}, EventId {EnduranceEventId}.",
            pendingDocuments.Count,
            pendingSnapshotCount,
            Context.ConnectionId,
            correlationId,
            enduranceEventId
        );

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

            stopwatch.Stop();
            _logger.LogInformation(
                "Judge pending snapshot flush completed in {ElapsedMilliseconds} ms. Flushed {PendingSnapshotCount} snapshot groups for event {EnduranceEventId}. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}.",
                stopwatch.ElapsedMilliseconds,
                pendingSnapshotCount,
                enduranceEventId,
                Context.ConnectionId,
                correlationId
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Failed to flush {count} pending witness snapshots for event '{enduranceEventId}' after {ElapsedMilliseconds} ms. Keeping them persisted. ConnectionId {ConnectionId}, CorrelationId {CorrelationId}.",
                pendingSnapshotCount,
                enduranceEventId,
                stopwatch.ElapsedMilliseconds,
                Context.ConnectionId,
                correlationId
            );
        }
    }
}
