using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Watcher;
using System.Diagnostics;

namespace NTS.Nexus.Warp.Features.Witness.PendingSnapshots;

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

    readonly ILogger<PendingSnapshotsMongoRepository> _logger;

    public PendingSnapshotsMongoRepository(
        IMongoContext context,
        ILogger<PendingSnapshotsMongoRepository> logger
    )
        : base(context, DATABASE, COLLECTION)
    {
        _logger = logger;
    }

    protected override UpdateDefinition<PendingSnapshotsModel> GetUpdateDefinition(PendingSnapshotsModel document)
    {
        throw new NotSupportedException("Pending snapshots are append-only and should not be updated.");
    }

    public async Task Create(string enduranceEventId, SnapshotGroupModel snapshotGroup)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await GetCollection()
                .InsertOneAsync(
                new PendingSnapshotsModel { EnduranceEventId = enduranceEventId, SnapshotGroups = [snapshotGroup] }
                );
            stopwatch.Stop();
            _logger.LogInformation(
                "Mongo create pending snapshots completed in {ElapsedMilliseconds} ms for event {EnduranceEventId}. SnapshotCount {SnapshotCount}.",
                stopwatch.ElapsedMilliseconds,
                enduranceEventId,
                snapshotGroup.Entries.Length
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Mongo create pending snapshots failed after {ElapsedMilliseconds} ms for event {EnduranceEventId}.",
                stopwatch.ElapsedMilliseconds,
                enduranceEventId
            );
            throw;
        }
    }

    public async Task<IReadOnlyList<PendingSnapshotsModel>> Read(string enduranceEventId)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var documents = await GetCollection().Find(x => x.EnduranceEventId == enduranceEventId).ToListAsync();
            stopwatch.Stop();
            _logger.LogInformation(
                "Mongo read pending snapshots completed in {ElapsedMilliseconds} ms for event {EnduranceEventId}. DocumentCount {DocumentCount}.",
                stopwatch.ElapsedMilliseconds,
                enduranceEventId,
                documents.Count
            );
            return documents;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Mongo read pending snapshots failed after {ElapsedMilliseconds} ms for event {EnduranceEventId}.",
                stopwatch.ElapsedMilliseconds,
                enduranceEventId
            );
            throw;
        }
    }

    public async Task Remove(PendingSnapshotsModel pendingSnapshots)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await GetCollection().DeleteOneAsync(x => x.MongoId == pendingSnapshots.MongoId);
            stopwatch.Stop();
            _logger.LogInformation(
                "Mongo delete pending snapshots completed in {ElapsedMilliseconds} ms for event {EnduranceEventId}. MongoId {MongoId}.",
                stopwatch.ElapsedMilliseconds,
                pendingSnapshots.EnduranceEventId,
                pendingSnapshots.MongoId
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Mongo delete pending snapshots failed after {ElapsedMilliseconds} ms for event {EnduranceEventId}. MongoId {MongoId}.",
                stopwatch.ElapsedMilliseconds,
                pendingSnapshots.EnduranceEventId,
                pendingSnapshots.MongoId
            );
            throw;
        }
    }
}
