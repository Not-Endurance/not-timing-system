using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Application.Watcher;

namespace NTS.Nexus.Warp.Features.Witness;

public interface IPendingSnapshotsService : ISingleton
{
    Task Append(string enduranceEventId, SnapshotGroupModel snapshotGroup);
    Task<IReadOnlyList<PendingSnapshotsModel>> Read(string enduranceEventId);
    Task Remove(PendingSnapshotsModel pendingSnapshots);
}

public sealed class PendingSnapshotsService : IPendingSnapshotsService
{
    readonly IPendingSnapshotsRepository _repository;

    public PendingSnapshotsService(IPendingSnapshotsRepository repository)
    {
        _repository = repository;
    }

    public async Task Append(string enduranceEventId, SnapshotGroupModel snapshotGroup)
    {
        if (snapshotGroup.Entries.Length == 0)
        {
            return;
        }

        await _repository.Create(enduranceEventId, snapshotGroup);
    }

    public async Task<IReadOnlyList<PendingSnapshotsModel>> Read(string enduranceEventId)
    {
        return await _repository.Read(enduranceEventId);
    }

    public async Task Remove(PendingSnapshotsModel pendingSnapshots)
    {
        await _repository.Remove(pendingSnapshots);
    }
}
