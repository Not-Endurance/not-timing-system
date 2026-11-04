using MediatR;
using NC.Application.RPC;
using NC.Application.RPC.Clients;
using NC.Application.RPC.SignalR;
using NC.Injection;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Application.Contracts.Watcher;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Objects.Payloads;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;
using NoTiming.EventBus.Contracts;
using NoTiming.EventBus.Contracts.Features.Judge.Procedures;
using NoTiming.Judge.Contracts.Features.Core.Dashboard;

namespace NoTiming.Judge.Features.Socket;

public class JudgeRpcClient
    : RpcClient,
        IJudgeClientProcedures,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationArrived>,
        INotificationHandler<InspectionRequired>,
        INotificationHandler<RepresentationRequired>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        IScoped
{
    readonly INoTimingSocketService _eventContext;
    readonly IJudgeSnapshotService _snapshotService;
    readonly HubProcedures _hubProcedures;

    public JudgeRpcClient(INoTimingSocketService eventContext, IRpcSocket socket, IJudgeSnapshotService snapshotService)
        : base(socket)
    {
        _eventContext = eventContext;
        _hubProcedures = new HubProcedures(socket);
        _snapshotService = snapshotService;
    }

    protected override void RegisterProcedures()
    {
        RegisterInputProcedure<SnapshotGroupModel>(nameof(Receive), Receive);
    }

    public async Task Receive(SnapshotGroupModel snapshots)
    {
        foreach (var watcherSnapshot in snapshots.Entries.Where(x => x.Timestamp != null))
        {
            var stamp = new Timestamp(watcherSnapshot.Timestamp!);
            var snapshot = new Snapshot(watcherSnapshot.Number, snapshots.Type, SnapshotMethod.Manual, stamp);
            await _snapshotService.Record(snapshot);
        }
    }

    public async Task Handle(PhaseCompleted completed, CancellationToken cancellationToken)
    {
        var request = NoTimingRequest.Create(GetGroupId(), completed);
        await _hubProcedures.OnPhaseCompleted(request);
    }

    public async Task Handle(ParticipationArrived arrived, CancellationToken cancellationToken)
    {
        var request = NoTimingRequest.Create(GetGroupId(), arrived);
        await _hubProcedures.OnParticipationArrived(request);
    }

    public async Task Handle(InspectionRequired inspectionRequired, CancellationToken cancellationToken)
    {
        var request = NoTimingRequest.Create(GetGroupId(), inspectionRequired);
        await _hubProcedures.OnInspectionRequired(request);
    }

    public async Task Handle(RepresentationRequired representationRequired, CancellationToken cancellationToken)
    {
        var request = NoTimingRequest.Create(GetGroupId(), representationRequired);
        await _hubProcedures.OnRepresentationRequired(request);
    }

    public async Task Handle(ParticipationEliminated eliminated, CancellationToken cancellationToken)
    {
        var request = NoTimingRequest.Create(GetGroupId(), eliminated);
        await _hubProcedures.OnParticipationEliminated(request);
    }

    public async Task Handle(ParticipationRestored restored, CancellationToken cancellationToken)
    {
        var request = NoTimingRequest.Create(GetGroupId(), restored);
        await _hubProcedures.OnParticipationRestored(request);
    }

    string GetGroupId()
    {
        return _eventContext.Event!.Id.ToString();
    }

    class HubProcedures : IJudgeHubProcedures
    {
        readonly IRpcSocket _socket;

        public HubProcedures(IRpcSocket socket)
        {
            _socket = socket;
        }

        public async Task OnPhaseCompleted(NoTimingRequest<PhaseCompleted> request)
        {
            await _socket.InvokeInputProcedure(nameof(OnPhaseCompleted), request);
        }

        public async Task OnParticipationArrived(NoTimingRequest<ParticipationArrived> request)
        {
            await _socket.InvokeInputProcedure(nameof(OnParticipationArrived), request);
        }

        public async Task OnInspectionRequired(NoTimingRequest<InspectionRequired> request)
        {
            await _socket.InvokeInputProcedure(nameof(OnInspectionRequired), request);
        }

        public async Task OnRepresentationRequired(NoTimingRequest<RepresentationRequired> request)
        {
            await _socket.InvokeInputProcedure(nameof(OnRepresentationRequired), request);
        }

        public async Task OnParticipationEliminated(NoTimingRequest<ParticipationEliminated> request)
        {
            await _socket.InvokeInputProcedure(nameof(OnParticipationEliminated), request);
        }

        public async Task OnParticipationRestored(NoTimingRequest<ParticipationRestored> request)
        {
            await _socket.InvokeInputProcedure(nameof(OnParticipationRestored), request);
        }
    }
}
