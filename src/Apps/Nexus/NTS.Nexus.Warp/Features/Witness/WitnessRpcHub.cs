using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;
using Not.Application.RPC.Clients;
using Not.Async.Extensions;
using NTS.Application.Core;
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

    public async Task<IEnumerable<ParticipationModel>> SendParticipations(WarpRequest request)
    {
        if (!TryGetJudgeClient(request.EnduranceEventId, out var judgeClient))
        {
            return [];
        }
        return await judgeClient.GetActive();
    }

    public async Task<RpcInvokeResult> Receive(WarpRequest<SnapshotModel> request)
    {
        if (!TryGetJudgeClient(request.EnduranceEventId, out var judgeClient))
        {
            return RpcInvokeResult.Error; // TODO: meaningful message would improve UX here
        }
        var payload = request.Payload.MapToDomain();
        var snapshots = payload.Entries.Select(entry => new Snapshot(
            entry.Number,
            payload.Type,
            Domain.Enums.SnapshotMethod.Manual,
            entry.Timestamp
        ));
        await judgeClient.Receive(snapshots);
        return RpcInvokeResult.Success;
    }

    bool TryGetJudgeClient(string enduranceEventId, [NotNullWhen(true)] out IJudgeClientProcedures? judgeClient)
    {
        var identifier = enduranceEventId.ToString();
        var connectionId = _primaryConnections.GetConnectionId(identifier);
        if (connectionId == null)
        {
            judgeClient = null;
            return false;
        }

        judgeClient = _judgeRelay.Clients.Client(connectionId);
        return true;
    }
}

