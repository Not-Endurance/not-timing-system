using MongoDB.Driver;
using NC.Application.Authentication.Abstractions;
using NC.Injection;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Application.Contracts.Watcher;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Application.UserSession;

namespace NoTiming.Api.Application.Mongo.Repositories;

public class UserSessionMongoRepository
    : MongoRepository<NoTimingUserSessionModel>,
        INoTimingUserSessionRepository,
        INUserSessionRepository<NoTimingUserSessionStateModel>,
        IEventResetRepository,
        ITransient
{
    readonly ITelemetryService _telemetry;

    public UserSessionMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.USER_SESSIONS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<NoTimingUserSessionModel> GetUpdateDefinition(NoTimingUserSessionModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(UserSessionMongoRepository), nameof(GetUpdateDefinition));

        return Builders<NoTimingUserSessionModel>
            .Update.Set(x => x.UserIdentifier, document.UserIdentifier)
            .Set(x => x.EventId, document.EventId)
            .Set(x => x.State, document.State);
    }

    protected override System.Linq.Expressions.Expression<Func<NoTimingUserSessionModel, bool>> GetItemFilter(
        NoTimingUserSessionModel item
    )
    {
        return x => x.Id == item.Id && x.EventId == item.EventId;
    }

    public async Task<NoTimingUserSessionModel?> ReadByUserIdentifier(string userIdentifier)
    {
        using var activity = _telemetry.StartActivity(nameof(UserSessionMongoRepository), nameof(ReadByUserIdentifier));

        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            return null;
        }

        return await GetCollection().Find(x => x.UserIdentifier == userIdentifier).FirstOrDefaultAsync();
    }

    public async Task<NoTimingUserSessionModel?> ReadByUserIdentifier(string userIdentifier, int eventId)
    {
        using var activity = _telemetry.StartActivity(nameof(UserSessionMongoRepository), nameof(ReadByUserIdentifier));

        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            return null;
        }

        return await GetCollection()
            .Find(x => x.UserIdentifier == userIdentifier && x.EventId == eventId)
            .FirstOrDefaultAsync();
    }

    public Task DeleteAllForEvent(int eventId)
    {
        return DeleteMany(x => x.EventId == eventId);
    }

    async Task<NoTimingUserSessionStateModel?> INUserSessionRepository<NoTimingUserSessionStateModel>.ReadByUserIdentifier(
        string userIdentifier
    )
    {
        return (await ReadByUserIdentifier(userIdentifier))?.State?.Copy();
    }
}
