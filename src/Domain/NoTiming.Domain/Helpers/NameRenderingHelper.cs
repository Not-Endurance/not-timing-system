namespace NoTiming.Domain.Helpers;

public static class NameRenderingHelper
{
    public static string Render(
        string name,
        string? nameEnglish,
        bool englishNames = false,
        CultureInfo? culture = null
    )
    {
        if (englishNames || ShouldUseEnglish(culture ?? CultureInfo.CurrentUICulture))
        {
            return FirstNonEmpty(nameEnglish, name);
        }

        return FirstNonEmpty(name, nameEnglish);
    }

    static bool ShouldUseEnglish(CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(culture.Name)
            || string.Equals(culture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);
    }

    static string FirstNonEmpty(string? preferred, string? fallback)
    {
        return string.IsNullOrWhiteSpace(preferred) ? fallback ?? string.Empty : preferred;
    }
}
