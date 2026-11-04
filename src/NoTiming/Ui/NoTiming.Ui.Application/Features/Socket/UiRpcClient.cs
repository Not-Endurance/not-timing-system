using Microsoft.AspNetCore.SignalR.Client;
using NC.Application.DomainEvents;
using NC.Application.RPC;
using NC.Application.RPC.Clients;
using NC.Exceptions;
using NC.Injection;
using NoTiming.Application.Contracts.Presentlists;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Application.Contracts.Watcher;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Core.Objects.Payloads;
using NoTiming.Domain.Core.Objects.Presentlists;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;
using NoTiming.Domain.Watcher;
using NoTiming.EventBus.Contracts;
using NoTiming.EventBus.Contracts.Features.Ui.Procedures;
using NoTiming.Ui.Application.Features.Core.Dashboard;

namespace NoTiming.Ui.Application.Features.Socket;

public class UiRpcClient : RpcClient, IUiClientProcedures, ISnapshotPublisher, IPresentlistActionPublisher, IScoped
{
    readonly IRpcSocket _socket;
    readonly INoTimingSocketContext _socketContext;
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public UiRpcClient(
        IRpcSocket socket,
        INoTimingSocketContext socketContext,
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

    protected virtual Task SendReceiveAsync(NoTimingRequest<SnapshotGroupModel> request)
    {
        return _socket.Connection!.InvokeAsync(nameof(IUiHubProcedures.Receive), request);
    }

    public async Task PublishSnapshotsAsync(SnapshotGroup snapshotGroup)
    {
        GuardHelper.ThrowIfDefault(_socket.Connection);
        var connectedEvent = GuardHelper.ThrowIfDefault(
            _socketContext.Event,
            "Cannot publish witness snapshots before connecting to an event."
        );

        var model = SnapshotGroupModel.MapFrom(snapshotGroup);
        var request = NoTimingRequest.Create(connectedEvent.Id.ToString(), model);
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

        var request = NoTimingRequest.Create(connectedEvent.Id.ToString(), acknoledgement);
        await _socket.Connection!.InvokeAsync(nameof(IUiHubProcedures.AcknoledgeVetIn), request);
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
