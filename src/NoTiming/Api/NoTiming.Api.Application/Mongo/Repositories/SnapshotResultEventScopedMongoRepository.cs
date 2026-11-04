using System.Diagnostics;
using MongoDB.Driver;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;

namespace NoTiming.Api.Application.Mongo.Repositories;

public class SnapshotResultEventScopedMongoRepository : EventScopedMongoRepository<SnapshotResultModel>
{
    readonly ITelemetryService _telemetry;

    public SnapshotResultEventScopedMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.SNAPSHOT_RESULTS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<SnapshotResultModel> GetUpdateDefinition(SnapshotResultModel document)
    {
        using var activity = _telemetry.StartActivity(
            nameof(SnapshotResultEventScopedMongoRepository),
            nameof(GetUpdateDefinition)
        );

        try
        {
            return Builders<SnapshotResultModel>
                .Update.Set(x => x.Snapshot, document.Snapshot)
                .Set(x => x.EventId, document.EventId)
                .Set(x => x.Type, document.Type);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
