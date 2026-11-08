using MongoDB.Bson.Serialization.Attributes;

namespace NC.Server.Authentication.Codes;

/// <summary>One challenge per normalized address. It holds the MAC of the code, never the code.</summary>
[BsonIgnoreExtraElements]
public sealed class CodeChallengeDocument
{
    /// <summary>The normalized address.</summary>
    [BsonId]
    public string Email { get; set; } = string.Empty;

    /// <summary>sign-in or register.</summary>
    public string Purpose { get; set; } = string.Empty;

    public byte[] Mac { get; set; } = [];

    public int AttemptsLeft { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastSentAt { get; set; }

    /// <summary>The TTL index deletes the challenge once this has passed.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }

    [BsonIgnoreIfNull]
    public RegistrationDetailsDocument? Registration { get; set; }
}
