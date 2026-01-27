using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;
using Not.Application.RPC.Clients;
using Not.Concurrency.Extensions;
using NTS.Application.Models;
using NTS.Domain.Aggregates;
using NTS.Warp.Features.Judge;
using NTS.Warp.Features.Judge.Procedures;
using NTS.Warp.Features.Witness.Procedures;

namespace NTS.Warp.Features.Witness;

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

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var enduranceEventId = GetConnectionGroup()!;
        if (!TryGetJudgeClient(enduranceEventId, out var judgeClient))
        {
            return;
        }
        await judgeClient.OnWitnessConnected(Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        var enduranceEventId = GetConnectionGroup()!;
        if (!TryGetJudgeClient(enduranceEventId, out var judgeClient))
        {
            return;
        }
        await judgeClient.OnWitnessDisconnected(Context.ConnectionId);
    }

    public async Task<IEnumerable<CoreParticipationModel>> SendParticipations(WarpRequest request)
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
