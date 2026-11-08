using MongoDB.Bson.Serialization.Attributes;

namespace NC.Server.Authentication.RateLimits;

/// <summary>A count of sends in one scope and one hour bucket. Expires two hours after the bucket starts.</summary>
[BsonIgnoreExtraElements]
public sealed class RateCounterDocument
{
    /// <summary>scope|key|yyyyMMddHH</summary>
    [BsonId]
    public string Key { get; set; } = string.Empty;

    public int Count { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }
}
