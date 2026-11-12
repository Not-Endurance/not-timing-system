using MongoDB.Driver;

namespace NC.Server.Authentication.Stores.MongoDB;

/// <summary>
/// Compares addresses without regard to case. A query with this collation uses the email index created with it, which a
/// regular expression cannot do. Legacy rows may still hold a mixed-case address, so lookups must use this collation.
/// </summary>
internal static class EmailCollation
{
    public static Collation Value { get; } = new("en", strength: CollationStrength.Secondary);
}
