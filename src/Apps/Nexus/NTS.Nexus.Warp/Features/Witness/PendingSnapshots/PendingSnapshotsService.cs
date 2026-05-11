using Not.Injection;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;

namespace NTS.Nexus.Warp.Features.Witness.PendingSnapshots;

public interface IPendingSnapshotsService
{
    Task Append(string eventId, SnapshotGroupModel snapshotGroup);
    Task<IReadOnlyList<PendingSnapshotsModel>> Read(string eventId);
    Task Remove(PendingSnapshotsModel pendingSnapshots);
}

public sealed class PendingSnapshotsService : IPendingSnapshotsService, ITransient
{
    readonly IPendingSnapshotsRepository _pendingSnapshots;

    public PendingSnapshotsService(IPendingSnapshotsRepository pendingSnapshots)
    {
        _pendingSnapshots = pendingSnapshots;
    }

    public async Task Append(string eventId, SnapshotGroupModel snapshotGroup)
    {
        if (snapshotGroup.Entries.Length == 0)
        {
            return;
        }

        await _pendingSnapshots.Create(eventId, snapshotGroup);
    }

    public async Task<IReadOnlyList<PendingSnapshotsModel>> Read(string eventId)
    {
        return await _pendingSnapshots.Read(eventId);
    }

    public async Task Remove(PendingSnapshotsModel pendingSnapshots)
    {
        await _pendingSnapshots.Remove(pendingSnapshots);
    }
}
