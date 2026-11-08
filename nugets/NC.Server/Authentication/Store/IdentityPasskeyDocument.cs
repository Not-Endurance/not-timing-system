using MongoDB.Bson.Serialization.Attributes;

namespace NC.Server.Authentication.Store;

/// <summary>A passkey, embedded in its user's document with the fields Identity needs to check an assertion.</summary>
[BsonIgnoreExtraElements]
public sealed class IdentityPasskeyDocument
{
    public byte[] CredentialId { get; set; } = [];

    public byte[] PublicKey { get; set; } = [];

    [BsonIgnoreIfNull]
    public string? Name { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastUsedAt { get; set; }

    public long SignCount { get; set; }

    public string[] Transports { get; set; } = [];

    public bool IsUserVerified { get; set; }

    public bool IsBackupEligible { get; set; }

    public bool IsBackedUp { get; set; }

    public byte[] AttestationObject { get; set; } = [];

    public byte[] ClientDataJson { get; set; } = [];
}
