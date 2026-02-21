using MediatR;
using Not.Application.CRUD.Ports;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Async.Extensions;
using Not.Injection;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Features.Core.Dashboard;
using NTS.Warp;
using NTS.Warp.Features.Judge.Procedures;

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
    readonly IReadMany<Participation> _coreParticipations;
    readonly HubProcedures _hubProcedures;

    public JudgeRpcClient(
        INtsSocketService eventContext,
        IRpcSocket socket,
        ITimingService timingService,
        IReadMany<Participation> coreParticipations
    )
        : base(socket)
    {
        _hubProcedures = new HubProcedures(socket);
        _eventContext = eventContext;
        _timingService = timingService;
        _coreParticipations = coreParticipations;
    }

    public override void RunAtStartup()
    {
        RegisterInputProcedure<IEnumerable<Snapshot>>(nameof(Receive), Receive);
        RegisterOutputCollectionProcedure(nameof(GetActive), GetActive);
    }

    public async Task Receive(IEnumerable<Snapshot> snapshots)
    {
        foreach (Snapshot snapshot in snapshots)
        {
            await _timingService.Record(snapshot);
        }
    }

    /// <summary>
    /// Fetches active participations after Competitions are started.
    /// </summary>
    /// <returns>Collection of active (not eliminated or completed) participations</returns>
    public async Task<IEnumerable<ParticipationModel>> GetActive()
    {
        var coreParticipations = await _coreParticipations
            .ReadMany(x => !x.IsComplete() && !x.IsEliminated())
            .Select(ParticipationModel.MapFrom);
        return coreParticipations;
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        return OnPhaseCompleted(notification);
    }

    public Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        return OnParticipationEliminated(notification);
    }

    public Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        return OnParticipationRestored(notification);
    }

    public async Task OnParticipationEliminated(ParticipationEliminated eliminated)
    {
        var request = WarpRequest.Create(GetGroupId(), eliminated);
        await _hubProcedures.OnParticipationEliminated(request);
    }

    public async Task OnParticipationRestored(ParticipationRestored restored)
    {
        var request = WarpRequest.Create(GetGroupId(), restored);
        await _hubProcedures.OnParticipationRestored(request);
    }

    public async Task OnPhaseCompleted(PhaseCompleted phaseCompleted)
    {
        var request = WarpRequest.Create(GetGroupId(), phaseCompleted);
        await _hubProcedures.OnPhaseCompleted(request);
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
