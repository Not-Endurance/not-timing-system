using Microsoft.AspNetCore.SignalR.Client;
using Not.Application.DomainEvents;
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

    public async Task PublishSnapshotsAsync(SnapshotGroup snapshotGroup)
    {
        GuardHelper.ThrowIfDefault(_eventContext.Event);
        GuardHelper.ThrowIfDefault(_socket.Connection);

        var model = SnapshotGroupModel.MapFrom(snapshotGroup);
        var request = WarpRequest.Create(_eventContext.Event.Id.ToString(), model);
        await _socket.Connection.InvokeAsync(nameof(IWitnessHubProcedures.Receive), request);
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
