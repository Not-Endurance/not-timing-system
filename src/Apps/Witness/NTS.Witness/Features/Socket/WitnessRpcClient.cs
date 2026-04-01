using Microsoft.AspNetCore.SignalR.Client;
using Not.Application.DomainEvents;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Exceptions;
using Not.Injection;
using NTS.Application.Socket;
using NTS.Application.Watcher;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Watcher;
using NTS.Nexus.Warp.Contracts;
using NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Features.Socket;

public class WitnessRpcClient : RpcClient, IWitnessClientProcedures, ISnapshotPublisher, IScoped
{
    readonly IRpcSocket _socket;
    readonly INtsSocketContext _socketContext;
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public WitnessRpcClient(
        IRpcSocket socket,
        INtsSocketContext socketContext,
        IDomainEventDispatcher domainEventDispatcher
    )
        : base(socket)
    {
        _socket = socket;
        _socketContext = socketContext;
        _domainEventDispatcher = domainEventDispatcher;
    }

    protected override void RegisterProcedures()
    {
        RegisterInputProcedure<PhaseCompleted>(nameof(OnPhaseCompleted), OnPhaseCompleted);
        RegisterInputProcedure<ParticipationEliminated>(nameof(OnParticipationEliminated), OnParticipationEliminated);
        RegisterInputProcedure<ParticipationRestored>(nameof(OnParticipationRestored), OnParticipationRestored);
    }

    protected virtual Task SendReceiveAsync(WarpRequest<SnapshotGroupModel> request)
    {
        return _socket.Connection!.InvokeAsync(nameof(IWitnessHubProcedures.Receive), request);
    }

    public async Task PublishSnapshotsAsync(SnapshotGroup snapshotGroup)
    {
        GuardHelper.ThrowIfDefault(_socket.Connection);
        var connectedEvent = GuardHelper.ThrowIfDefault(
            _socketContext.Event,
            "Cannot publish witness snapshots before connecting to an event."
        );

        var model = SnapshotGroupModel.MapFrom(snapshotGroup);
        var request = WarpRequest.Create(connectedEvent.Id.ToString(), model);
        await SendReceiveAsync(request);
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
