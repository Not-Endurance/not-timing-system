using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NC.Server.Authentication.Sessions;
using NC.Server.Authentication.Settings;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.Stores.MongoDB.Repositories;

internal sealed class SessionRepository : ISessionRepository
{
    readonly IMongoCollection<SessionDocument> _sessions;

    public SessionRepository(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _sessions = context
            .Client.GetDatabase(settings.Database)
            .GetCollection<SessionDocument>(settings.SessionsCollection);
    }

    public Task<SessionDocument?> FindLive(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return _sessions
            .Find(x => x.TokenHash == tokenHash && x.ExpiresAtUtc > now.UtcDateTime)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task Create(
        string tokenHash,
        int userId,
        string securityStamp,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    )
    {
        return _sessions.InsertOneAsync(
            new SessionDocument
            {
                TokenHash = tokenHash,
                UserId = userId,
                SecurityStamp = securityStamp,
                CreatedAtUtc = now.UtcDateTime,
                LastExtendedAtUtc = now.UtcDateTime,
                ExpiresAtUtc = SessionRules.ExpiresAt(now).UtcDateTime,
            },
            cancellationToken: cancellationToken
        );
    }

    /// <summary>Extends the session from now. Without an upsert, a session revoked meanwhile stays revoked.</summary>
    public Task Extend(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return _sessions.UpdateOneAsync(
            x => x.TokenHash == tokenHash,
            Builders<SessionDocument>
                .Update.Set(x => x.LastExtendedAtUtc, now.UtcDateTime)
                .Set(x => x.ExpiresAtUtc, SessionRules.ExpiresAt(now).UtcDateTime),
            cancellationToken: cancellationToken
        );
    }

    public Task Remove(string tokenHash, CancellationToken cancellationToken = default)
    {
        return _sessions.DeleteOneAsync(x => x.TokenHash == tokenHash, cancellationToken);
    }

    /// <summary>Ends every session of a user. The security stamp rotation calls this.</summary>
    public Task RemoveAllForUser(int userId, CancellationToken cancellationToken = default)
    {
        return _sessions.DeleteManyAsync(x => x.UserId == userId, cancellationToken);
    }
}
