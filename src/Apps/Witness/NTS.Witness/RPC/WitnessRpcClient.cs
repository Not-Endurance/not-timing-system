using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Collections;
using Not.Injection;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Application.Startlists;
using NTS.Application.Watcher;
using NTS.Domain.Core.Aggregates;
using NTS.Warp;
using NTS.Warp.Features.Witness.Procedures;
using NTS.Witness.Services;

namespace NTS.Witness.RPC;

public class WitnessRpcClient : RpcClient, IWitnessClientProcedures, IParticipationGetter, ISnapshotService, ISingleton
{
    readonly IRpcSocket _socket;
    readonly INtsSocketService _eventContext;
    readonly IStartlistContext _startlistContext;
    readonly IParticipationService _participationService;

    public WitnessRpcClient(
        IRpcSocket socket,
        INtsSocketService eventContext,
        IParticipationService participationService,
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
        RegisterInputProcedure<Participation>(nameof(ReceiveParticipation), ReceiveParticipation);
    }

    public async Task<RpcInvokeResult> PublishSnapshotsAsync(SnapshotModel model)
    {
        var request = WarpRequest.Create(_eventContext.Event!.Id.ToString(), model);
        return await _socket.InvokeInputProcedure(nameof(IWitnessHubProcedures.Receive), request);
    }

    public Task ReceiveParticipation(Participation participation)
    {
        if (participation.IsEliminated())
        {
            _participationService.Update(participation, NCollectionAction.Remove);
            _startlistContext.Update(participation, NCollectionAction.Remove);
            return Task.CompletedTask;
        }
        if (participation.Phases.Current.IsComplete())
        {
            if (participation.Phases.Current.IsFinal)
            {
                _participationService.Update(participation, NCollectionAction.Remove);
                return Task.CompletedTask;
            }
        }
        _participationService.Update(participation, NCollectionAction.AddOrUpdate);
        _startlistContext.Update(participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public async Task GetParticipations()
    {
        var request = WarpRequest.Create(_eventContext.Event!.Id.ToString());
        var result = await _socket.InvokeInputOutputProcedure<WarpRequest, IEnumerable<ParticipationModel>>(
            nameof(IWitnessHubProcedures.SendParticipations),
            request
        );
        if (result.Data != null)
        {
            _participationService.Set(result.Data.Select(x => x.MapToDomain()));
        }
    }
}
