using System.Text;

namespace NTS.Application.Contracts.Pdf;

public static class PdfFileNameHelper
{
    static readonly char[] INVALID_FILE_NAME_CHARS =
    [
        .. Path.GetInvalidFileNameChars(),
        '<',
        '>',
        ':',
        '"',
        '/',
        '\\',
        '|',
        '?',
        '*',
    ];

    public static string HandoutsPdf(int eventId)
    {
        return $"handouts-{eventId}.pdf";
    }

    public static string RanklistPdf(int rankingId, string? name)
    {
        return $"{SanitizeName(name, $"ranklist-{rankingId}")}.pdf";
    }

    public static string ResultsZip(int eventId)
    {
        return $"results-{eventId}.zip";
    }

    public static IReadOnlyList<(PdfNamedResult Result, string EntryName)> ResultPdfEntries(
        IEnumerable<PdfNamedResult> results
    )
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<(PdfNamedResult Result, string EntryName)>();

        foreach (var result in results)
        {
            var baseName = SanitizeName(result.Name, $"ranklist-{result.Id}");
            counts.TryGetValue(baseName, out var count);
            counts[baseName] = count + 1;

            var fileName = count == 0 ? $"{baseName}.pdf" : $"{baseName}-{result.Id}.pdf";
            entries.Add((result, fileName));
        }

        return entries;
    }

    public static string SanitizeName(string? name, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        var builder = new StringBuilder(normalized.Length);
        var lastWasSeparator = false;

        foreach (var character in normalized)
        {
            var safeCharacter = ResolveSafeCharacter(character);
            if (safeCharacter == '-')
            {
                if (lastWasSeparator)
                {
                    continue;
                }
                lastWasSeparator = true;
            }
            else
            {
                lastWasSeparator = false;
            }

            builder.Append(safeCharacter);
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    static char ResolveSafeCharacter(char character)
    {
        if (INVALID_FILE_NAME_CHARS.Contains(character) || char.IsWhiteSpace(character))
        {
            return '-';
        }

        return character;
    }
}
