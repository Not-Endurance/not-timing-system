using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Not.Identity;

/// <summary>
/// A passkey, embedded in the document of its user with its id and its key as binary (ADR-0002). Every field of
/// <c>UserPasskeyInfo</c> is kept, so a credential read back is the credential that was stored. A passkey is replaced
/// in place each time it signs in, because its sign count moves.
/// </summary>
public sealed class NIdentityPasskey
{
    /// <summary>The credential id the authenticator chose. Unique across users, see the partial unique index.</summary>
    public byte[] CredentialId { get; set; } = [];

    public byte[] PublicKey { get; set; } = [];

    /// <summary>What the person calls it: "iPhone", "Work laptop".</summary>
    [BsonIgnoreIfNull]
    public string? Name { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset CreatedAt { get; set; }

    [BsonRepresentation(BsonType.Int64)]
    public uint SignCount { get; set; }

    public string[] Transports { get; set; } = [];
    public bool IsUserVerified { get; set; }
    public bool IsBackupEligible { get; set; }
    public bool IsBackedUp { get; set; }
    public byte[] AttestationObject { get; set; } = [];
    public byte[] ClientDataJson { get; set; } = [];
}
