using Not.Application.DomainEvents;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Injection;
using NTS.Application.Socket;
using NTS.Application.Watcher;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Nexus.Warp.Contracts;
using NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Features.Socket;

public class WitnessRpcClient : RpcClient, IWitnessClientProcedures, ISnapshotService, ISingleton
{
    readonly IRpcSocket _socket;
    readonly INtsSocketService _eventContext;
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public WitnessRpcClient(
        IRpcSocket socket,
        INtsSocketService eventContext,
        IDomainEventDispatcher domainEventDispatcher
    )
        : base(socket)
    {
        _socket = socket;
        _eventContext = eventContext;
        _domainEventDispatcher = domainEventDispatcher;
    }

    public override void RunAtStartup()
    {
        RegisterInputProcedure<PhaseCompleted>(nameof(OnPhaseCompleted), OnPhaseCompleted);
        RegisterInputProcedure<ParticipationEliminated>(nameof(OnParticipationEliminated), OnParticipationEliminated);
        RegisterInputProcedure<ParticipationRestored>(nameof(OnParticipationRestored), OnParticipationRestored);
    }

    public async Task<RpcInvokeResult> PublishSnapshotsAsync(SnapshotModel model)
    {
        var @event = _eventContext.Event;
        if (@event == null)
        {
            return RpcInvokeResult.Error;
        }

        var request = WarpRequest.Create(@event.Id.ToString(), model);
        return await _socket.InvokeInputProcedure(nameof(IWitnessHubProcedures.Receive), request);
    }

    public Task OnPhaseCompleted(PhaseCompleted payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }

    public Task OnParticipationEliminated(ParticipationEliminated payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }

    public Task OnParticipationRestored(ParticipationRestored payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }
}
