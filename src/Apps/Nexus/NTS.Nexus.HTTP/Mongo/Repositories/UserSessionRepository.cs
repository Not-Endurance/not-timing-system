using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Watcher;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class UserSessionRepository : MongoRepository<UserSessionModel>
{
    readonly ITelemetryService _telemetry;

    public UserSessionRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.USER_SESSIONS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<UserSessionModel> GetUpdateDefinition(UserSessionModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(UserSessionRepository), nameof(GetUpdateDefinition));

        return Builders<UserSessionModel>
            .Update.Set(x => x.EventId, document.EventId)
            .Set(x => x.SnapshotHistory, document.SnapshotHistory);
    }
}
