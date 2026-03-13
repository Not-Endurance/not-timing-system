using MediatR;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Injection;
using NTS.Application.Watcher;
using NTS.Application.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Features.Core.Dashboard;
using NTS.Nexus.Warp.Contracts;
using NTS.Nexus.Warp.Contracts.Features.Judge.Procedures;

namespace NTS.Judge.Features.Socket;

public class JudgeRpcClient
    : RpcClient,
        IJudgeClientProcedures,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        ISingleton
{
    readonly INtsSocketService _eventContext;
    readonly ITimingService _timingService;
    readonly HubProcedures _hubProcedures;

    public JudgeRpcClient(
        INtsSocketService eventContext,
        IRpcSocket socket,
        ITimingService timingService
    )
        : base(socket)
    {
        _hubProcedures = new HubProcedures(socket);
        _eventContext = eventContext;
        _timingService = timingService;
    }

    public override void RunAtStartup()
    {
        RegisterInputProcedure<SnapshotGroupModel>(nameof(Receive), Receive);
    }

    public async Task Receive(SnapshotGroupModel snapshotGroup)
    {
        var group = snapshotGroup.MapToDomain();
        foreach (var entry in group.Entries)
        {
            await _timingService.Record(new Snapshot(entry.Number, group.Type, SnapshotMethod.Manual, entry.Timestamp));
        }
    }

    public async Task Handle(PhaseCompleted completed, CancellationToken cancellationToken)
    {
        var request = WarpRequest.Create(GetGroupId(), completed);
        await _hubProcedures.OnPhaseCompleted(request);
    }

    public async Task Handle(ParticipationEliminated eliminated, CancellationToken cancellationToken)
    {
        var request = WarpRequest.Create(GetGroupId(), eliminated);
        await _hubProcedures.OnParticipationEliminated(request);
    }

    public async Task Handle(ParticipationRestored restored, CancellationToken cancellationToken)
    {
        var request = WarpRequest.Create(GetGroupId(), restored);
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

        public async Task OnPhaseCompleted(WarpRequest<PhaseCompleted> request)
        {
            await _socket.InvokeInputProcedure(nameof(OnPhaseCompleted), request);
        }

        public async Task OnParticipationEliminated(WarpRequest<ParticipationEliminated> request)
        {
            await _socket.InvokeInputProcedure(nameof(OnParticipationEliminated), request);
        }

        public async Task OnParticipationRestored(WarpRequest<ParticipationRestored> request)
        {
            await _socket.InvokeInputProcedure(nameof(OnParticipationRestored), request);
        }
    }
}
