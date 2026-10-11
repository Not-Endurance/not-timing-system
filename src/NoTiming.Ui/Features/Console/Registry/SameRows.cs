using System.Globalization;
using System.Text.RegularExpressions;
using NTS.Contracts.Features.Console.Setup;

namespace NoTiming.Ui.Features.Console.Registry;

/// <summary>
/// What makes two rows of the registry the same Athlete or Horse as far as a form can tell (#646, ADR-0010): the same FEI ID, or
/// the same name once trimmed, with its spaces collapsed and its case folded, compared across the name and the name in English
/// of both. It is the exact part of the rules only; the spelling variants and near-misses of ADR-0010 are for the matching of
/// its migration, and #635 puts one service behind both.
/// </summary>
public static partial class SameRows
{
    /// <summary>The rows of those given that the one being registered is a second copy of, by its FEI ID first, with the reason that decided; none is the row itself.</summary>
    public static IReadOnlyList<SameRow> Among(
        IEnumerable<SameRow> candidates,
        Guid? itself,
        string? name,
        string? nameEnglish,
        string? feiId,
        bool feiIdIsNumber
    )
    {
        var names = Names(name, nameEnglish);
        var wanted = FeiIdOf(feiId, feiIdIsNumber);
        var found = new List<SameRow>();
        foreach (var row in candidates.Where(x => x.Id != itself))
        {
            if (wanted != null && FeiIdOf(row.FeiId, feiIdIsNumber) == wanted)
            {
                found.Add(Of(row, SameRowReason.FeiId));
            }
            else if (names.Count > 0 && Names(row.Name, row.NameEnglish).Overlaps(names))
            {
                found.Add(Of(row, SameRowReason.Name));
            }
        }

        return [.. found.OrderByDescending(x => x.Reason).ThenByDescending(x => x.IsOwn)];
    }

    /// <summary>The names a row is known by, each trimmed, with runs of spaces made one and its case folded; none for a name that is empty.</summary>
    public static HashSet<string> Names(string? name, string? nameEnglish)
    {
        return
        [
            .. new[] { name, nameEnglish }
                .Select(x => Spaces().Replace((x ?? string.Empty).Trim(), " ").ToUpperInvariant())
                .Where(x => x.Length > 0),
        ];
    }

    /// <summary>
    /// The FEI ID to compare by: an Athlete's as a number, so that <c>00012</c> is <c>12</c>, a Horse's as the text it is, case-folded.
    /// None for one that is empty, is zero, or is a placeholder made only of the same character that is not a digit.
    /// </summary>
    public static string? FeiIdOf(string? feiId, bool isNumber)
    {
        var text = (feiId ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (isNumber && text.All(char.IsAsciiDigit))
        {
            var number = text.TrimStart('0');
            return number.Length == 0 ? null : number;
        }

        var folded = text.ToUpper(CultureInfo.InvariantCulture);
        return folded.Distinct().Count() == 1 && !char.IsAsciiLetterOrDigit(folded[0]) ? null : folded;
    }

    static SameRow Of(SameRow row, SameRowReason reason)
    {
        return new SameRow(row.Id, row.Name, row.NameEnglish, row.FeiId, row.IsOwn, reason);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
