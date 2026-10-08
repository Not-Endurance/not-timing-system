using System.Globalization;

namespace NoTiming.Ui.Features.Account;

/// <summary>A language the app is written in, named in itself, so that a person who cannot read the app finds theirs.</summary>
public sealed class Language
{
    public Language(string code, string name)
    {
        Code = code;
        Name = name;
    }

    /// <summary>The two letters of the culture, such as <c>bg</c>.</summary>
    public string Code { get; }

    public string Name { get; }
}

/// <summary>
/// The languages the app is written in (#645): English, which every text falls back to, Bulgarian and Turkish. The choice of
/// one is a preference of the browser, kept there and applied when the app starts; it is not the account's.
/// </summary>
public static class Languages
{
    /// <summary>The language a stored choice names, none when it names none of those the app is written in.</summary>
    public static string? Chosen(string? stored)
    {
        var code = stored?.Trim().ToLowerInvariant();
        return Supported.FirstOrDefault(x => x.Code == code)?.Code;
    }

    /// <summary>The language the app is in under a culture: the one the culture is of, or English.</summary>
    public static string CodeOf(CultureInfo culture)
    {
        return Chosen(culture.TwoLetterISOLanguageName) ?? "en";
    }

    public static IReadOnlyList<Language> Supported { get; } =
        [new("en", "English"), new("bg", "Български"), new("tr", "Türkçe")];
}
