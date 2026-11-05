using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NoTiming.Api.Application.Authentication.Store;

/// <summary>
/// The identity fields of a row in the <c>users</c> collection. Identity extends the existing user documents in place:
/// the store writes only the fields below, by name, so the application's own fields (names, club, roles, event
/// roles that reference the integer <see cref="Id"/>) are neither read into writes nor lost. Unknown fields are ignored.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class IdentityUserDocument
{
    public int Id { get; set; }

    /// <summary>Lower case. The address is what a person signs in with.</summary>
    [BsonIgnoreIfNull]
    public string? Email { get; set; }

    /// <summary>
    /// When the address was proved by a code. Null on a row from before identity, and unique per address only once it
    /// is set, so legacy duplicates are allowed until they are proved.
    /// </summary>
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ProvedAt { get; set; }

    /// <summary>The random WebAuthn user handle. It is never the integer <see cref="Id"/>.</summary>
    [BsonIgnoreIfNull]
    public byte[]? UserHandle { get; set; }

    /// <summary>Changing it ends every session of the user.</summary>
    [BsonIgnoreIfNull]
    public string? SecurityStamp { get; set; }

    [BsonIgnoreIfNull]
    public string? ConcurrencyStamp { get; set; }

    public bool LockoutEnabled { get; set; }

    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LockoutEnd { get; set; }

    public int AccessFailedCount { get; set; }

    /// <summary>The passkeys of the user. The field is removed when the last one goes, so the credential index stays sparse.</summary>
    [BsonIgnoreIfNull]
    public List<IdentityPasskeyDocument>? Passkeys { get; set; }

    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public string? GivenName { get; set; }

    public string? MiddleName { get; set; }

    public string? Surname { get; set; }

    public string? CountryRegion { get; set; }

    public string? Club { get; set; }

    public string? FeiId { get; set; }

    public string[] Roles { get; set; } = [];

    /// <summary>The tenant every user is written with. Nothing reads it yet: tenant authorization is out of scope.</summary>
    public string TenantId { get; set; } = "nts";

    public bool IsProved => ProvedAt != null;
}

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
