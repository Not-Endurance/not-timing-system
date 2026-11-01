using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// The texts that tell things apart in the migration (#636). A key of a decision is made of the normalised spellings and the FEI
/// ID and of no database id, so that it is the same on the next run, on a restored copy and after the ids are Guids.
/// </summary>
internal static class IdentityKeys
{
    const char SEPARATOR = '\u001f';

    /// <summary>What copies agree on when they are one spelling: a Setup row is told by its document, as it is a spelling of its own.</summary>
    public static string SpellingOf(IdentityCopy copy)
    {
        return string.Join(
            SEPARATOR,
            copy.IsSetupRow ? $"setup:{copy.DocumentId}" : "copy",
            copy.LegacyId,
            copy.Name,
            copy.NameEnglish,
            copy.FeiId
        );
    }

    /// <summary>
    /// The key by which assignments find a spelling: a copy by what it says, and a Setup row by its document alone, so that a row that
    /// someone has corrected since the plan was made is still the row it was.
    /// </summary>
    public static string Told(string spellingKey)
    {
        var end = spellingKey.IndexOf(SEPARATOR);
        return spellingKey.StartsWith("setup:", StringComparison.Ordinal) && end >= 0
            ? spellingKey[..end]
            : spellingKey;
    }

    /// <summary>
    /// The key of a group: the kind, the FEI IDs it has (as numbers for an Athlete) and every spelling of its names, normalised,
    /// in order. <c>athlete (fei 100): ivan petrov | иван петров</c>.
    /// </summary>
    public static string GroupOf(IdentityKind kind, IEnumerable<IdentitySpelling> spellings)
    {
        var members = spellings.ToList();
        var fei = members
            .Select(x => IdentityFeiIds.Of(kind, x.FeiId))
            .Where(x => x != null)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
        var names = members
            .SelectMany(x => new[] { x.Copies[0].Name, x.Copies[0].NameEnglish })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => IdentityNames.Normalised(x).ToLowerInvariant())
            .Distinct()
            .Order(StringComparer.Ordinal);
        var identifiers = fei.Count == 0 ? "" : $" (fei {string.Join(", ", fei)})";
        return $"{kind.ToString().ToLowerInvariant()}{identifiers}: {string.Join(" | ", names)}";
    }

    /// <summary>The key of a pair of groups: the keys of the two, the smaller first, so that it does not matter which is which.</summary>
    public static string PairOf(string one, string another)
    {
        return string.CompareOrdinal(one, another) <= 0 ? $"{one} <> {another}" : $"{another} <> {one}";
    }
}
