using System.Globalization;

namespace NTS.Domain.Setup.Services.Identities;

/// <summary>The FEI ID that two copies are compared by (#635, ADR-0010).</summary>
public static class IdentityFeiIds
{
    /// <summary>
    /// The FEI ID to compare by: an Athlete's as a number, so that <c>00012</c> is <c>12</c>, a Horse's as the text it is, trimmed
    /// and case-folded. None for one that is empty, is zero, or is a placeholder made only of one character that is neither a
    /// letter nor a digit (<c>-----</c>).
    /// </summary>
    public static string? Of(IdentityKind kind, string? feiId)
    {
        var text = (feiId ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var folded = text.ToUpper(CultureInfo.InvariantCulture);
        if (folded.All(x => x == '0'))
        {
            return null;
        }

        if (kind == IdentityKind.Athlete && folded.All(char.IsAsciiDigit))
        {
            return folded.TrimStart('0');
        }

        return folded.Distinct().Count() == 1 && !char.IsAsciiLetterOrDigit(folded[0]) ? null : folded;
    }
}
