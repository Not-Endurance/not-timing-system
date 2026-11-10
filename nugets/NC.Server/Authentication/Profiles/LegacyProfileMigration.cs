using MongoDB.Bson;

namespace NC.Server.Authentication.Profiles;

/// <summary>
/// Moves the profile fields that users used to carry at the top level of their row into the <c>profile</c> document. A row
/// that has none of them is left alone, so the migration can run on every start.
/// </summary>
internal static class LegacyProfileMigration
{
    const string PROFILE = "profile";
    static readonly string[] LegacyNames = ["Club", "FeiId", "CountryRegion"];

    /// <summary>The migrated row, or null when the row has nothing to migrate. A value already in the profile is kept.</summary>
    public static BsonDocument? Migrate(BsonDocument row)
    {
        if (!LegacyNames.Any(row.Contains))
        {
            return null;
        }

        var migrated = row.DeepClone().AsBsonDocument;
        var profile =
            migrated.TryGetValue(PROFILE, out var existing) && existing.IsBsonDocument
                ? existing.AsBsonDocument
                : new BsonDocument();

        CopyIfMissing(row, profile, "Club", "club");
        CopyIfMissing(row, profile, "FeiId", "feiId");
        CopyIfMissing(row, profile, "CountryRegion", "countryIsoCode");

        migrated[PROFILE] = profile;
        foreach (var name in LegacyNames)
        {
            migrated.Remove(name);
        }

        return migrated;
    }

    static void CopyIfMissing(BsonDocument row, BsonDocument profile, string legacyName, string profileName)
    {
        if (!profile.Contains(profileName) && row.TryGetValue(legacyName, out var value))
        {
            profile[profileName] = value;
        }
    }
}
