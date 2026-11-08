using MongoDB.Bson.Serialization.Attributes;

namespace NC.Server.Authentication.Email;

[BsonIgnoreExtraElements]
public sealed class OutboxMessageDocument
{
    public string To { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string TextBody { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime SentAtUtc { get; set; }
}
