using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Watcher;

namespace NTS.Nexus.Warp.Features.Witness;

public interface IPendingSnapshotsRepository
{
    Task Create(string enduranceEventId, SnapshotGroupModel snapshotGroup);
    Task<IReadOnlyList<PendingSnapshotsModel>> Read(string enduranceEventId);
    Task Remove(PendingSnapshotsModel pendingSnapshots);
}

public class PendingSnapshotsMongoRepository : MongoRepository<PendingSnapshotsModel>, IPendingSnapshotsRepository
{
    const string DATABASE = "nts";
    const string COLLECTION = "pendingSnapshots";

    public PendingSnapshotsMongoRepository(IMongoContext context)
        : base(context, DATABASE, COLLECTION) { }

    protected override UpdateDefinition<PendingSnapshotsModel> GetUpdateDefinition(PendingSnapshotsModel document)
    {
        throw new NotSupportedException("Pending snapshots are append-only and should not be updated.");
    }

    public Task Create(string enduranceEventId, SnapshotGroupModel snapshotGroup)
    {
        return GetCollection()
            .InsertOneAsync(
                new PendingSnapshotsModel { EnduranceEventId = enduranceEventId, SnapshotGroups = [snapshotGroup] }
            );
    }

    public async Task<IReadOnlyList<PendingSnapshotsModel>> Read(string enduranceEventId)
    {
        return await GetCollection().Find(x => x.EnduranceEventId == enduranceEventId).ToListAsync();
    }

    public Task Remove(PendingSnapshotsModel pendingSnapshots)
    {
        return GetCollection().DeleteOneAsync(x => x.MongoId == pendingSnapshots.MongoId);
    }
}
