using Not.Injection;
using NTS.Application.Watcher;

namespace NTS.Nexus.Warp.Features.Witness;

public interface IPendingSnapshotsService
{
    Task Append(string enduranceEventId, SnapshotGroupModel snapshotGroup);
    Task<IReadOnlyList<PendingSnapshotsModel>> Read(string enduranceEventId);
    Task Remove(PendingSnapshotsModel pendingSnapshots);
}

public sealed class PendingSnapshotsService : IPendingSnapshotsService, ITransient
{
    readonly IPendingSnapshotsRepository _pendingSnapshots;

    public PendingSnapshotsService(IPendingSnapshotsRepository pendingSnapshots)
    {
        _pendingSnapshots = pendingSnapshots;
    }

    public async Task Append(string enduranceEventId, SnapshotGroupModel snapshotGroup)
    {
        if (snapshotGroup.Entries.Length == 0)
        {
            return;
        }

        await _pendingSnapshots.Create(enduranceEventId, snapshotGroup);
    }

    public async Task<IReadOnlyList<PendingSnapshotsModel>> Read(string enduranceEventId)
    {
        return await _pendingSnapshots.Read(enduranceEventId);
    }

    public async Task Remove(PendingSnapshotsModel pendingSnapshots)
    {
        await _pendingSnapshots.Remove(pendingSnapshots);
    }
}
