using System.Text;

namespace Not.Files;

public static class NFileNameHelper
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

    public static string Sanitize(string? name, string fallback)
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

    public static bool IsSafeFileName(string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && Path.GetFileName(fileName) == fileName
            && fileName.IndexOfAny(INVALID_FILE_NAME_CHARS) < 0;
    }

    static char ResolveSafeCharacter(char character)
    {
        return INVALID_FILE_NAME_CHARS.Contains(character) || char.IsWhiteSpace(character)
            ? '-'
            : character;
    }
}
