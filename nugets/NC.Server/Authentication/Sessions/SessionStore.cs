using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NC.Server.Authentication.Passkeys;
using NC.Server.Authentication.Store;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.Sessions;

public sealed class SessionStore : IPasskeySessionStore
{
    readonly IMongoCollection<SessionDocument> _sessions;

    public SessionStore(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _sessions = context
            .Client.GetDatabase(settings.Database)
            .GetCollection<SessionDocument>(settings.SessionsCollection);
    }

    public Task<SessionDocument?> FindLiveAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    )
    {
        return _sessions
            .Find(x => x.TokenHash == tokenHash && x.ExpiresAt > now.UtcDateTime)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task CreateAsync(
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
                CreatedAt = now.UtcDateTime,
                LastExtendedAt = now.UtcDateTime,
                ExpiresAt = SessionRules.ExpiresAt(now).UtcDateTime,
            },
            cancellationToken: cancellationToken
        );
    }

    /// <summary>Extends the session from now. Without an upsert, a session revoked meanwhile stays revoked.</summary>
    public Task ExtendAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return _sessions.UpdateOneAsync(
            x => x.TokenHash == tokenHash,
            Builders<SessionDocument>
                .Update.Set(x => x.LastExtendedAt, now.UtcDateTime)
                .Set(x => x.ExpiresAt, SessionRules.ExpiresAt(now).UtcDateTime),
            cancellationToken: cancellationToken
        );
    }

    public Task RemoveAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return _sessions.DeleteOneAsync(x => x.TokenHash == tokenHash, cancellationToken);
    }

    /// <summary>Ends every session of a user. The security stamp rotation calls this.</summary>
    public Task RemoveAllForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _sessions.DeleteManyAsync(x => x.UserId == userId, cancellationToken);
    }
}
