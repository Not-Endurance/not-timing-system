using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Not.Identity;

/// <summary>
/// The identity fields of a user document (ADR-0002). The document is the application's own user document, extended
/// in place: every other field of it is the application's and is neither read nor written here. The store updates the
/// fields below by name, so a field it does not know survives every write, and <see cref="OtherFields"/> keeps those
/// fields readable.
/// </summary>
public class NIdentityUser
{
    [BsonId]
    public Guid Id { get; set; }

    /// <summary>
    /// Lower case. It is the user name, the email and the normalized email at once, so the rows that exist before
    /// identity do not need a backfill.
    /// </summary>
    [BsonIgnoreIfNull]
    public string? Email { get; set; }

    /// <summary>False for every row that exists before identity: an address is confirmed by a code that reached it.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// Changing it ends every session of the user. A row from before identity has none, and Identity refuses to update
    /// a user without one: give it one with <c>UpdateSecurityStampAsync</c> before any other update, as the sign-in does.
    /// </summary>
    [BsonIgnoreIfNull]
    public string? SecurityStamp { get; set; }

    /// <summary>Every update names the stamp it was based on, and a stale one fails instead of being lost.</summary>
    [BsonIgnoreIfNull]
    public string? ConcurrencyStamp { get; set; }

    public bool LockoutEnabled { get; set; }

    [BsonIgnoreIfNull]
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? LockoutEnd { get; set; }

    public int AccessFailedCount { get; set; }

    /// <summary>Reserved for a federated sign-in: the provider and the subject it knows the person by.</summary>
    [BsonIgnoreIfNull]
    public string? ExternalProvider { get; set; }

    [BsonIgnoreIfNull]
    public string? ExternalSubject { get; set; }

    /// <summary>
    /// The passkeys of the user. The unique index on the credential id is partial, so a user without a passkey is left
    /// out of it: an update that leaves none unsets the field, and a user created through the store starts with an
    /// empty array, which has no credential id to index.
    /// </summary>
    [BsonIgnoreIfNull]
    public List<NIdentityPasskey> Passkeys { get; set; } = [];

    /// <summary>The application's own fields of the document, as they were loaded.</summary>
    [BsonExtraElements]
    public BsonDocument? OtherFields { get; set; }

    /// <summary>
    /// A text field of the application's own part of the document, which identity does not know. Null when there is
    /// no such field, when it is not a text or when it is blank.
    /// </summary>
    public string? TextOf(string field)
    {
        return
            OtherFields != null
            && OtherFields.TryGetValue(field, out var value)
            && value.IsString
            && !string.IsNullOrWhiteSpace(value.AsString)
            ? value.AsString
            : null;
    }
}
