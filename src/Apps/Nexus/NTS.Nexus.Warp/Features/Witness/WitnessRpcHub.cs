using Microsoft.AspNetCore.SignalR;
using NTS.Application.Watcher;
using NTS.Domain.Aggregates;
using NTS.Nexus.Warp.Contracts;
using NTS.Nexus.Warp.Contracts.Features.Judge.Procedures;
using NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;
using NTS.Nexus.Warp.Features.Judge;

namespace NTS.Nexus.Warp.Features.Witness;

internal class WitnessRpcHub : NtsHub<IWitnessClientProcedures>, IWitnessHubProcedures
{
    readonly IPrimaryConnectionContext _primaryConnections;
    readonly IHubContext<JudgeRpcHub, IJudgeClientProcedures> _judgeRelay;

    public WitnessRpcHub(
        ILogger<WitnessRpcHub> logger,
        IPrimaryConnectionContext primaryConnections,
        IHubContext<JudgeRpcHub, IJudgeClientProcedures> judgeRelay
    )
        : base(logger)
    {
        _primaryConnections = primaryConnections;
        _judgeRelay = judgeRelay;
    }

    public async Task Receive(WarpRequest<SnapshotModel> request)
    {
        var rpcClient = GetRpcClient(request.EnduranceEventId);
        var payload = request.Payload.MapToDomain();
        var snapshots = payload.Entries.Select(entry => new Snapshot(
            entry.Number,
            payload.Type,
            Domain.Enums.SnapshotMethod.Manual,
            entry.Timestamp
        ));
        await rpcClient.Receive(snapshots);
    }

    IJudgeClientProcedures GetRpcClient(string enduranceEventId)
    {
        if (string.IsNullOrWhiteSpace(enduranceEventId))
        {
            throw new InvalidOperationException("Message cannot be sent - you're not connected to an Event");
        }

        var identifier = enduranceEventId.ToString();
        var connectionId = _primaryConnections.GetConnectionId(identifier);
        if (connectionId == null)
        {
            // TODO: Implement a queue, which stores, persists and requeues messages when a primary listener connects
            // That messages that are send are never lost an clients are free to operate
            throw new InvalidOperationException("Message cannot be sent - No one is connected to that evet");
        }

        return _judgeRelay.Clients.Client(connectionId);
    }
}
