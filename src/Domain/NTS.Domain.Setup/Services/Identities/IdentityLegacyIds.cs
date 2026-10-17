namespace NTS.Domain.Setup.Services.Identities;

/// <summary>The legacy id that two copies are compared by (#635, ADR-0009, ADR-0010): what a copy was called before ids were Guids.</summary>
public static class IdentityLegacyIds
{
    /// <summary>
    /// The id to compare by: the text of the number without the zeros it was written with. None for one that is empty or is zero,
    /// because 0 is what a copy had when it had no id.
    /// </summary>
    public static string? Of(string? legacyId)
    {
        var text = (legacyId ?? string.Empty).Trim();
        if (text.All(x => x == '0'))
        {
            return null;
        }

        return text.All(char.IsAsciiDigit) ? text.TrimStart('0') : text;
    }
}
