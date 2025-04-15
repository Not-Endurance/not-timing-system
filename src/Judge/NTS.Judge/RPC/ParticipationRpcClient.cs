using Not.Application.CRUD.Ports;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using Not.Async;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Objects;
using NTS.Judge.Core;
using NTS.Warp.Features.Judge.Models;
using NTS.Warp.Features.Judge.Procedures;

namespace NTS.Judge.RPC;

public class ParticipationRpcClient : RpcClient, IParticipationRemoteProcedures
{
    readonly ISnapshotProcessor _snapshotProcessor;
    readonly IRead<Domain.Core.Aggregates.Participation> _coreParticipations;
    readonly IRead<Domain.Setup.Aggregates.Participation> _setupParticipations;

    public ParticipationRpcClient(
        IRpcSocket socket,
        ISnapshotProcessor snapshotProcessor,
        IRead<Domain.Core.Aggregates.Participation> coreParticipations,
        IRead<Domain.Setup.Aggregates.Participation> setupParticipations
    )
        : base(socket)
    {
        _snapshotProcessor = snapshotProcessor;
        _coreParticipations = coreParticipations;
        _setupParticipations = setupParticipations;
    }

    public override void RunAtStartup()
    {
        Domain.Core.Aggregates.Participation.PHASE_COMPLETED_EVENT.Subscribe(SendStartCreated);
        Domain.Core.Aggregates.Participation.ELIMINATED_EVENT.Subscribe(SendParticipationEliminated);
        Domain.Core.Aggregates.Participation.RESTORED_EVENT.Subscribe(SendParticipationRestored);

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
    public async Task<IEnumerable<ParticipationWarpDto>> GetActiveParticipations()
    {
        var coreParticipations = await _coreParticipations
            .ReadAll(x => !x.IsComplete() && !x.IsEliminated())
            .Select(ParticipationWarpDto.Create);
        if (coreParticipations.Any())
        {
            return coreParticipations;
        }

        var participations = await _setupParticipations.ReadAll();
        return participations.Select(ParticipationWarpDto.Create);
    }

    public async Task SendParticipationEliminated(ParticipationEliminated revoked)
    {
        await InvokeHubProcedure(nameof(IJudgeHubProcedures.SendParticipationEliminated), revoked);
    }

    public async Task SendParticipationRestored(ParticipationRestored restored)
    {
        await InvokeHubProcedure(nameof(IJudgeHubProcedures.SendParticipationRestored), restored);
    }

    public async Task SendStartCreated(PhaseCompleted phaseCompleted)
    {
        await InvokeHubProcedure(nameof(IJudgeHubProcedures.SendStartCreated), phaseCompleted);
    }
}
