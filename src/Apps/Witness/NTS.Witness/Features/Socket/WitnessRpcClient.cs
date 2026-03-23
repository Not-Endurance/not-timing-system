using Microsoft.AspNetCore.SignalR.Client;
using Not.Application.DomainEvents;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Exceptions;
using Not.Injection;
using NTS.Application.UserSession;
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
    readonly IWitnessUserSession _userSessionService;
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public WitnessRpcClient(
        IRpcSocket socket,
        IWitnessUserSession userSessionService,
        IDomainEventDispatcher domainEventDispatcher
    )
        : base(socket)
    {
        _socket = socket;
        _userSessionService = userSessionService;
        _domainEventDispatcher = domainEventDispatcher;
    }

    protected override void RegisterProcedures()
    {
        RegisterInputProcedure<PhaseCompleted>(nameof(OnPhaseCompleted), OnPhaseCompleted);
        RegisterInputProcedure<ParticipationEliminated>(nameof(OnParticipationEliminated), OnParticipationEliminated);
        RegisterInputProcedure<ParticipationRestored>(nameof(OnParticipationRestored), OnParticipationRestored);
    }

    public async Task PublishSnapshotsAsync(SnapshotGroup snapshotGroup)
    {
        var session = await _userSessionService.GetCurrent();
        GuardHelper.ThrowIfDefault(session?.EventId);
        GuardHelper.ThrowIfDefault(_socket.Connection);

        var model = SnapshotGroupModel.MapFrom(snapshotGroup);
        var request = WarpRequest.Create(session.EventId.Value.ToString(), model);
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
