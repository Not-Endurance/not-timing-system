using Microsoft.AspNetCore.SignalR;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Core.Objects.Payloads;
using NoTiming.EventBus.Abstractions;
using NoTiming.EventBus.Contracts;
using NoTiming.EventBus.Contracts.Features.Judge.Procedures;
using NoTiming.EventBus.Contracts.Features.Witness.Procedures;
using NoTiming.EventBus.Features.Judge;
using NoTiming.Api.Application.Features.Witness.Authorization;
using NoTiming.Api.Application.Features.Witness.PendingSnapshots;

namespace NoTiming.EventBus.Features.Witness;

internal class WitnessRpcHub : NtsHub<IWitnessClientProcedures>, IWitnessHubProcedures
{
    readonly IJudgeConnectionsContext _primaryConnections;
    readonly IHubContext<JudgeRpcHub, IJudgeClientProcedures> _judgeRelay;
    readonly IPendingSnapshotsService _pendingSnapshots;
    readonly IWitnessReceiveAuthorizer _receiveAuthorizer;

    public WitnessRpcHub(
        ILogger<WitnessRpcHub> logger,
        IJudgeConnectionsContext primaryConnections,
        IHubContext<JudgeRpcHub, IJudgeClientProcedures> judgeRelay,
        IPendingSnapshotsService pendingSnapshots,
        IWitnessReceiveAuthorizer receiveAuthorizer
    )
        : base(logger)
    {
        _primaryConnections = primaryConnections;
        _judgeRelay = judgeRelay;
        _pendingSnapshots = pendingSnapshots;
        _receiveAuthorizer = receiveAuthorizer;
    }

    public async Task Receive(WarpRequest<SnapshotGroupModel> request)
    {
        var eventId = GetEventId(request.EventId);
        await _receiveAuthorizer.Authorize(Context.User, GetConnectionGroup(), eventId);
        if (request.Payload.Entries.Length == 0)
        {
            return;
        }

        var connectionId = _primaryConnections.GetConnectionId(eventId);
        if (connectionId == null)
        {
            await _pendingSnapshots.Append(eventId, request.Payload);
            return;
        }

        await _judgeRelay.Clients.Client(connectionId).Receive(request.Payload);
    }

    public async Task AcknoledgeVetIn(WarpRequest<VetInAcknoledged> request)
    {
        var eventId = GetEventId(request.EventId);
        await _receiveAuthorizer.Authorize(Context.User, GetConnectionGroup(), eventId);
        await Clients.Group(eventId).OnPresentationAcknoledged(request.Payload);
    }

    static string GetEventId(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new HubException("Message cannot be sent because no event is selected.");
        }

        return eventId;
    }
}
