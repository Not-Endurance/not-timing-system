using MongoDB.Bson;

namespace NC.Server.Authentication.Identity;

/// <summary>
/// The identity fields of a row in the <c>users</c> collection, and the app's profile, which the module stores whole. The
/// repository writes only the fields below, by name, so the application's own fields (names, roles, event roles that
/// reference the integer <see cref="Id"/>) are neither read into writes nor lost. Unknown fields are ignored.
/// </summary>
internal sealed class IdentityUserDocument
{
    public int Id { get; set; }

    /// <summary>Lower case. The address is what a person signs in with.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// When the address was proved by a code. Null on a row from before identity, and unique per address only once it
    /// is set, so legacy duplicates are allowed until they are proved.
    /// </summary>
    public DateTime? ProvedAtUtc { get; set; }

    /// <summary>The random WebAuthn user handle. It is never the integer <see cref="Id"/>.</summary>
    public byte[]? UserHandle { get; set; }

    /// <summary>Changing it ends every session of the user.</summary>
    public string? SecurityStamp { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public bool LockoutEnabled { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public int AccessFailedCount { get; set; }

    /// <summary>The passkeys of the user. The field is removed when the last one goes, so the credential index stays sparse.</summary>
    public List<IdentityPasskeyDocument>? Passkeys { get; set; }

    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public string? GivenName { get; set; }

    public string? MiddleName { get; set; }

    public string? Surname { get; set; }

    /// <summary>
    /// The app's profile as the row holds it. The value is typed loosely, so a malformed profile reads as an empty one
    /// instead of failing the read of every user. <c>ProfileDocument</c> does the reading.
    /// </summary>
    public BsonValue? Profile { get; set; }

    public string[] Roles { get; set; } = [];

    /// <summary>The tenant every user is written with. Nothing reads it yet: tenant authorization is out of scope.</summary>
    public string TenantId { get; set; } = "nts";

    public bool IsProved => ProvedAtUtc != null;
}
