using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Authentication.Store;

namespace NoTiming.Api.Application.Authentication.Sessions;

/// <summary>A server-side session. Only the hash of the token is stored, so a copy of the database cannot be replayed.</summary>
[BsonIgnoreExtraElements]
public sealed class SessionDocument
{
    [BsonId]
    public string TokenHash { get; set; } = string.Empty;

    public int UserId { get; set; }

    /// <summary>The user's security stamp when the session began. A changed stamp ends the session.</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastExtendedAt { get; set; }

    /// <summary>The TTL index deletes the session once this has passed.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }
}

public sealed class SessionStore
{
    readonly IMongoCollection<SessionDocument> _sessions;

    public SessionStore(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _sessions = context.Client.GetDatabase(settings.Database).GetCollection<SessionDocument>(settings.SessionsCollection);
    }

    public Task<SessionDocument?> FindLiveAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return _sessions
            .Find(x => x.TokenHash == tokenHash && x.ExpiresAt > now.UtcDateTime)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task CreateAsync(string tokenHash, int userId, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken = default)
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
            Builders<SessionDocument>.Update
                .Set(x => x.LastExtendedAt, now.UtcDateTime)
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
