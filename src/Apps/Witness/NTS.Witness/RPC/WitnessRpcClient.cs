using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Collections;
using NTS.Application.Models;
using NTS.Application.Startlists;
using NTS.Application.Warp;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Warp;
using NTS.Warp.Features.Witness.Procedures;
using NTS.Witness.Services;

namespace NTS.Witness.RPC;

public class WitnessRpcClient
    : RpcClient,
        IWitnessClientProcedures,
        IClientParticipationGetter,
        ISnapshotService
{
    readonly IRpcSocket _socket;
    readonly ISelectedEventContext _eventContext;
    readonly IStartlistContext _startlistContext;
    readonly ParticipationService _participationService;

    public WitnessRpcClient(
        IRpcSocket socket,
        ISelectedEventContext eventContext,
        ParticipationService participationService,
        IStartlistContext startlistContext
    )
        : base(socket)
    {
        _socket = socket;
        _eventContext = eventContext;
        _startlistContext = startlistContext;
        _participationService = participationService;
    }

    public override void RunAtStartup()
    {
        RegisterInputProcedure<StartlistEntry, NCollectionAction>(nameof(ReceiveStartlistEntry), ReceiveStartlistEntry);
        RegisterInputProcedure<Participation, NCollectionAction>(nameof(ReceiveParticipation), ReceiveParticipation);
    }

    public async Task<RpcInvokeResult> PublishSnapshotsAsync(SnapshotModel model)
    {
        var request = WarpRequest.Create(_eventContext.Event!.Id.ToString(), model);
        return await _socket.InvokeInputProcedure(nameof(IWitnessHubProcedures.Receive), request);
    }

    public Task ReceiveStartlistEntry(StartlistEntry entry, NCollectionAction action)
    {
        _startlistContext.Update(entry, action);
        return Task.CompletedTask;
    }

    public Task ReceiveParticipation(Participation participation, NCollectionAction action)
    {
        _participationService.Update(participation, action);
        return Task.CompletedTask;
    }

    public async Task GetParticipations()
    {
        var request = WarpRequest.Create(_eventContext.Event!.Id.ToString());
        var result = await _socket.InvokeInputOutputProcedure<IEnumerable<Participation>, WarpRequest>(
            nameof(IWitnessHubProcedures.SendParticipations),
            request
        );
        if (result.Data != null)
        {
            _participationService.Active = result.Data;
        }
    }
}
