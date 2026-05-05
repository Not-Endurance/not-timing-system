using MongoDB.Driver;
using Not.Application.Authentication.Abstractions;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Application.UserSession;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class UserSessionMongoRepository
    : MongoRepository<NtsUserSessionModel>,
        INtsUserSessionRepository,
        INUserSessionRepository<NtsUserSessionStateModel>
{
    readonly ITelemetryService _telemetry;

    public UserSessionMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.USER_SESSIONS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<NtsUserSessionModel> GetUpdateDefinition(NtsUserSessionModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(UserSessionMongoRepository), nameof(GetUpdateDefinition));

        return Builders<NtsUserSessionModel>
            .Update.Set(x => x.UserIdentifier, document.UserIdentifier)
            .Set(x => x.State, document.State);
    }

    public async Task<NtsUserSessionModel?> ReadByUserIdentifier(string userIdentifier)
    {
        using var activity = _telemetry.StartActivity(nameof(UserSessionMongoRepository), nameof(ReadByUserIdentifier));

        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            return null;
        }

        return await GetCollection().Find(x => x.UserIdentifier == userIdentifier).FirstOrDefaultAsync();
    }

    async Task<NtsUserSessionStateModel?> INUserSessionRepository<NtsUserSessionStateModel>.ReadByUserIdentifier(
        string userIdentifier
    )
    {
        return (await ReadByUserIdentifier(userIdentifier))?.State?.Copy();
    }
}
