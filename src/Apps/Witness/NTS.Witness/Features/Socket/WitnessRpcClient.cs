using Microsoft.AspNetCore.SignalR.Client;
using Not.Application.DomainEvents;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Exceptions;
using Not.Injection;
using NTS.Application.Contracts.Presentlists;
using NTS.Application.Contracts.Socket;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Core.Objects.Presentlists;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using NTS.Nexus.Warp.Contracts;
using NTS.Nexus.Warp.Contracts.Features.Witness.Procedures;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Features.Socket;

public class WitnessRpcClient
    : RpcClient,
        IWitnessClientProcedures,
        ISnapshotPublisher,
        IPresentlistActionPublisher,
        IScoped
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
        RegisterInputProcedure<ParticipationArrived>(nameof(OnParticipationArrived), OnParticipationArrived);
        RegisterInputProcedure<InspectionRequired>(nameof(OnInspectionRequired), OnInspectionRequired);
        RegisterInputProcedure<RepresentationRequired>(nameof(OnRepresentationRequired), OnRepresentationRequired);
        RegisterInputProcedure<PhaseCompleted>(nameof(OnPhaseCompleted), OnPhaseCompleted);
        RegisterInputProcedure<ParticipationEliminated>(nameof(OnParticipationEliminated), OnParticipationEliminated);
        RegisterInputProcedure<ParticipationRestored>(nameof(OnParticipationRestored), OnParticipationRestored);
        RegisterInputProcedure<VetInAcknoledged>(nameof(OnPresentationAcknoledged), OnPresentationAcknoledged);
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

    public async Task PublishPresentation(PresentlistEntry entry)
    {
        var snapshot = new Snapshot(
            entry.Number,
            entry.AthleteName,
            entry.AthleteNameEnglish,
            Timestamp.Now(),
            entry.Ruleset
        );
        await PublishSnapshotsAsync(new SnapshotGroup([snapshot], SnapshotType.Present));
    }

    public async Task PublishPresentationAcknoledged(VetInAcknoledged acknoledgement)
    {
        GuardHelper.ThrowIfDefault(_socket.Connection);
        var connectedEvent = GuardHelper.ThrowIfDefault(
            _socketContext.Event,
            "Cannot acknowledge presentation before connecting to an event."
        );

        var request = WarpRequest.Create(connectedEvent.Id.ToString(), acknoledgement);
        await _socket.Connection!.InvokeAsync(nameof(IWitnessHubProcedures.AcknoledgeVetIn), request);
    }

    public Task OnPhaseCompleted(PhaseCompleted payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }

    public Task OnParticipationArrived(ParticipationArrived payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }

    public Task OnInspectionRequired(InspectionRequired payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }

    public Task OnRepresentationRequired(RepresentationRequired payload)
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

    public Task OnPresentationAcknoledged(VetInAcknoledged payload)
    {
        return _domainEventDispatcher.Dispatch(payload);
    }
}
