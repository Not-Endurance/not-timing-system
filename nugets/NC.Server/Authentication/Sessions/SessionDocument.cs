using MongoDB.Bson.Serialization.Attributes;

namespace NC.Server.Authentication.Sessions;

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
