using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Not.Identity.Codes;

internal sealed class CodeChallenge
{
    [BsonId]
    public Guid Id { get; set; }

    /// <summary>The normalized address.</summary>
    public string Email { get; set; } = default!;

    public string Purpose { get; set; } = default!;

    /// <summary>The code, protected with the data protection key ring. It is never stored in the clear.</summary>
    public string ProtectedCode { get; set; } = default!;

    public int Attempts { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    /// <summary>The TTL index deletes the document once this has passed.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }

    [BsonIgnoreIfNull]
    public BsonDocument? Pending { get; set; }
}
