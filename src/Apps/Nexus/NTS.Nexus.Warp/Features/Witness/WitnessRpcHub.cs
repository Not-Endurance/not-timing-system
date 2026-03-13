using Microsoft.AspNetCore.SignalR;
using NTS.Application.Watcher;
using NTS.Nexus.Warp.Contracts;
using NTS.Nexus.Warp.Contracts.Features.Judge.Procedures;
using NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;
using NTS.Nexus.Warp.Features.Judge;

namespace NTS.Nexus.Warp.Features.Witness;

internal class WitnessRpcHub : NtsHub<IWitnessClientProcedures>, IWitnessHubProcedures
{
    readonly IPrimaryConnectionContext _primaryConnections;
    readonly IHubContext<JudgeRpcHub, IJudgeClientProcedures> _judgeRelay;
    readonly IPendingSnapshotsService _pendingSnapshots;

    public WitnessRpcHub(
        ILogger<WitnessRpcHub> logger,
        IPrimaryConnectionContext primaryConnections,
        IHubContext<JudgeRpcHub, IJudgeClientProcedures> judgeRelay,
        IPendingSnapshotsService pendingSnapshots
    )
        : base(logger)
    {
        _primaryConnections = primaryConnections;
        _judgeRelay = judgeRelay;
        _pendingSnapshots = pendingSnapshots;
    }

    public async Task Receive(WarpRequest<SnapshotGroupModel> request)
    {
        var enduranceEventId = GetEnduranceEventId(request.EnduranceEventId);
        if (request.Payload.Entries.Length == 0)
        {
            return;
        }

        var connectionId = _primaryConnections.GetConnectionId(enduranceEventId);
        if (connectionId == null)
        {
            await _pendingSnapshots.Append(enduranceEventId, request.Payload);
            return;
        }

        await _judgeRelay.Clients.Client(connectionId).Receive(request.Payload);
    }

    static string GetEnduranceEventId(string enduranceEventId)
    {
        if (string.IsNullOrWhiteSpace(enduranceEventId))
        {
            throw new InvalidOperationException("Message cannot be sent - you're not connected to an Event");
        }

        return enduranceEventId;
    }
}
