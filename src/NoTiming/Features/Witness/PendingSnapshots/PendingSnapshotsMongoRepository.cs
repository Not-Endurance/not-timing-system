using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;

namespace NTS.Nexus.Warp.Features.Witness.PendingSnapshots;

public interface IPendingSnapshotsRepository
{
    Task Create(string eventId, SnapshotGroupModel snapshotGroup);
    Task<IReadOnlyList<PendingSnapshotsModel>> Read(string eventId);
    Task Remove(PendingSnapshotsModel pendingSnapshots);
}

public class PendingSnapshotsMongoRepository : MongoRepository<PendingSnapshotsModel>, IPendingSnapshotsRepository
{
    const string DATABASE = "nts";
    const string COLLECTION = "event_pending_snapshots";

    readonly ILogger<PendingSnapshotsMongoRepository> _logger;

    public PendingSnapshotsMongoRepository(IMongoContext context, ILogger<PendingSnapshotsMongoRepository> logger)
        : base(context, DATABASE, COLLECTION)
    {
        _logger = logger;
    }

    protected override UpdateDefinition<PendingSnapshotsModel> GetUpdateDefinition(PendingSnapshotsModel document)
    {
        throw new NotSupportedException("Pending snapshots are append-only and should not be updated.");
    }

    public async Task Create(string eventId, SnapshotGroupModel snapshotGroup)
    {
        var parsedEventId = ParseEventId(eventId);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await GetCollection()
                .InsertOneAsync(
                    new PendingSnapshotsModel { EventId = parsedEventId, SnapshotGroups = [snapshotGroup] }
                );
            stopwatch.Stop();
            _logger.LogInformation(
                "Mongo create pending snapshots completed in {ElapsedMilliseconds} ms for event {EventId}. SnapshotCount {SnapshotCount}.",
                stopwatch.ElapsedMilliseconds,
                parsedEventId,
                snapshotGroup.Entries.Length
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Mongo create pending snapshots failed after {ElapsedMilliseconds} ms for event {EventId}.",
                stopwatch.ElapsedMilliseconds,
                parsedEventId
            );
            throw;
        }
    }

    public async Task<IReadOnlyList<PendingSnapshotsModel>> Read(string eventId)
    {
        var parsedEventId = ParseEventId(eventId);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var documents = await GetCollection().Find(x => x.EventId == parsedEventId).ToListAsync();
            stopwatch.Stop();
            _logger.LogInformation(
                "Mongo read pending snapshots completed in {ElapsedMilliseconds} ms for event {EventId}. DocumentCount {DocumentCount}.",
                stopwatch.ElapsedMilliseconds,
                parsedEventId,
                documents.Count
            );
            return documents;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Mongo read pending snapshots failed after {ElapsedMilliseconds} ms for event {EventId}.",
                stopwatch.ElapsedMilliseconds,
                parsedEventId
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
                "Mongo delete pending snapshots completed in {ElapsedMilliseconds} ms for event {EventId}. MongoId {MongoId}.",
                stopwatch.ElapsedMilliseconds,
                pendingSnapshots.EventId,
                pendingSnapshots.MongoId
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Mongo delete pending snapshots failed after {ElapsedMilliseconds} ms for event {EventId}. MongoId {MongoId}.",
                stopwatch.ElapsedMilliseconds,
                pendingSnapshots.EventId,
                pendingSnapshots.MongoId
            );
            throw;
        }
    }

    static int ParseEventId(string eventId)
    {
        if (int.TryParse(eventId, out var parsedEventId))
        {
            return parsedEventId;
        }

        throw new ArgumentException($"Event id '{eventId}' is not a valid integer.", nameof(eventId));
    }
}
