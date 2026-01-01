using Not.Application.CRUD.Ports;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Async;
using NTS.Application.Models;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Objects;
using NTS.Judge.Core;
using NTS.Judge.Features.Warp;
using NTS.Warp;
using NTS.Warp.Features.Judge.Procedures;

namespace NTS.Judge.RPC;

public class ParticipationRpcClient : RpcClient, IParticipationClientProcedures
{
    readonly IEventContext _eventContext;
    readonly ISnapshotProcessor _snapshotProcessor;
    readonly IRead<Domain.Core.Aggregates.Participation> _coreParticipations;
    readonly HubProcedures _hubProcedures;

    public ParticipationRpcClient(
        IEventContext eventContext,
        IRpcSocket socket,
        ISnapshotProcessor snapshotProcessor,
        IRead<Domain.Core.Aggregates.Participation> coreParticipations
    )
        : base(socket)
    {
        _hubProcedures = new HubProcedures(socket);
        _eventContext = eventContext;
        _snapshotProcessor = snapshotProcessor;
        _coreParticipations = coreParticipations;
    }

    public override void RunAtStartup()
    {
        Domain.Core.Aggregates.Participation.PHASE_COMPLETED_EVENT.Subscribe(OnPhaseCompleted);
        Domain.Core.Aggregates.Participation.ELIMINATED_EVENT.Subscribe(OnParticipationEliminated);
        Domain.Core.Aggregates.Participation.RESTORED_EVENT.Subscribe(OnParticipationRestored);

        RegisterInputProcedure<IEnumerable<Snapshot>>(nameof(ProcessSnapshots), ProcessSnapshots);
        RegisterOutputCollectionProcedure(nameof(GetActiveParticipations), GetActiveParticipations);
    }

    public async Task ProcessSnapshots(IEnumerable<Snapshot> snapshots)
    {
        foreach (Snapshot snapshot in snapshots)
        {
            await _snapshotProcessor.Process(snapshot);
        }
    }

    /// <summary>
    /// Fetches active participations before and after Competitions are started.
    /// </summary>
    /// <returns>Collection of active (not eliminated or completed) participations</returns>
    public async Task<IEnumerable<CoreParticipationModel>> GetActiveParticipations()
    {
        var coreParticipations = await _coreParticipations
            .ReadAll(x => !x.IsComplete() && !x.IsEliminated())
            .Select(CoreParticipationModel.MapFrom);
        return coreParticipations;
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

    class HubProcedures : IParticipationHubProcedures
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
