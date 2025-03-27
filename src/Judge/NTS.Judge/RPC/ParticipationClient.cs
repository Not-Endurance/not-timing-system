using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using NTS.Application.RPC;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Objects;
using NTS.Judge.Core;

namespace NTS.Judge.RPC;

public class ParticipationClient : RpcClient, IParticipationRpcClient
{
    readonly ISnapshotProcessor _snapshotProcessor;

    public ParticipationClient(
        IRpcSocket socket,
        ISnapshotProcessor snapshotProcessor
    )
        : base(socket)
    {
        _snapshotProcessor = snapshotProcessor;
        RegisterClientProcedure<IEnumerable<Snapshot>>(
            nameof(IJudgeClientProcedures.ReceiveSnapshots),
            ReceiveSnapshots
        );
    }

    public override void RunAtStartup()
    {
        Participation.PHASE_COMPLETED_EVENT.Subscribe(SendStartCreated);
        Participation.ELIMINATED_EVENT.Subscribe(SendParticipationEliminated);
        Participation.RESTORED_EVENT.Subscribe(SendParticipationRestored);
    }

    public async Task ReceiveSnapshots(IEnumerable<Snapshot> snapshots)
    {
        foreach (Snapshot snapshot in snapshots)
        {
            await _snapshotProcessor.Process(snapshot);
        }
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

public interface IParticipationRpcClient : IParticipationClientProcedures, IRpcClient { }
