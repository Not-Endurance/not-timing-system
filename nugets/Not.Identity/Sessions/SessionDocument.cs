using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Not.Identity.Sessions;

internal sealed class SessionDocument
{
    /// <summary>The key the cookie carries.</summary>
    [BsonId]
    public string Id { get; set; } = default!;

    public Guid UserId { get; set; }

    /// <summary>The serialized authentication ticket: the principal and its properties.</summary>
    public byte[] Ticket { get; set; } = [];

    [BsonElement("CreatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>The TTL index deletes the document once this has passed.</summary>
    [BsonElement("ExpiresAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAtUtc { get; set; }
}
