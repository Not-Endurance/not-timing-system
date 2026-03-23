using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.UserSession;
using NTS.Application.Watcher;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class UserSessionRepository : MongoRepository<NtsUserSessionModel>, INtsUserSessionRepository
{
    readonly ITelemetryService _telemetry;

    public UserSessionRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.USER_SESSIONS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<NtsUserSessionModel> GetUpdateDefinition(NtsUserSessionModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(UserSessionRepository), nameof(GetUpdateDefinition));

        return Builders<NtsUserSessionModel>
            .Update.Set(x => x.UserIdentifier, document.UserIdentifier)
            .Set(x => x.EventId, document.EventId)
            .Set(x => x.SnapshotHistory, document.SnapshotHistory);
    }

    public async Task<NtsUserSessionModel?> ReadByUserIdentifier(string userIdentifier)
    {
        using var activity = _telemetry.StartActivity(nameof(UserSessionRepository), nameof(ReadByUserIdentifier));

        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            return null;
        }

        return await GetCollection().Find(x => x.UserIdentifier == userIdentifier).FirstOrDefaultAsync();
    }
}
