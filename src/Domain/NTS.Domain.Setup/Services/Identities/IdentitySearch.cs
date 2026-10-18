namespace NTS.Domain.Setup.Services.Identities;

/// <summary>
/// Whether a query finds an Athlete or a Horse by a name (#638, ADR-0010). The query and the names are compared by the looser key
/// of <see cref="IdentityNames"/>, which is in Latin letters whatever script the name was written in, so a Latin query finds a name
/// in Cyrillic and the other way round, and the spellings of one name are one. A query is found in a name when each of its words is
/// in the name, in any order and as a part of a word, so that a few letters of each word are enough.
/// </summary>
public static class IdentitySearch
{
    /// <summary>
    /// Whether every word of the query is part of one of the names. A query with nothing in it finds nothing, and neither does a row
    /// with no name.
    /// </summary>
    /// <param name="query">What was typed.</param>
    /// <param name="names">The names the row has: its name and its name in English.</param>
    public static bool Matches(string? query, params string?[] names)
    {
        var words = IdentityNames.Looser(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0
            && names.Any(name =>
                !string.IsNullOrWhiteSpace(name)
                && IdentityNames.Looser(name) is var key
                && words.All(word => key.Contains(word, StringComparison.Ordinal))
            );
    }
}
