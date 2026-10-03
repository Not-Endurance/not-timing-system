using System.Text.Json;
using System.Text.RegularExpressions;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// The text of the sign-in pages and of the emails, in the languages of the platform. English is the fallback for a
/// language that is not supported and for a key a translation lacks.
/// </summary>
public sealed class AccountText
{
    public const string FALLBACK = "en";

    public static AccountText Load()
    {
        var assembly = typeof(AccountText).Assembly;
        var text = new Dictionary<string, Dictionary<string, string>>();
        foreach (var language in Languages)
        {
            using var stream =
                assembly.GetManifestResourceStream($"account/text/{language}.json")
                ?? throw new InvalidOperationException($"The text of the language '{language}' is not embedded.");
            text[language] =
                JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? throw new InvalidOperationException($"The text of the language '{language}' is empty.");
        }

        return new AccountText(text);
    }

    /// <summary>Replaces <c>{name}</c> in a text with the values given.</summary>
    public static string Fill(string text, IReadOnlyDictionary<string, string> values)
    {
        return Regex.Replace(
            text,
            @"\{([A-Za-z]+)\}",
            match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value
        );
    }

    readonly Dictionary<string, Dictionary<string, string>> _text;

    AccountText(Dictionary<string, Dictionary<string, string>> text)
    {
        _text = text;
    }

    public static IReadOnlyList<string> Languages { get; } = ["en", "bg", "tr"];

    public IReadOnlyCollection<string> Keys(string language)
    {
        return _text[language].Keys;
    }

    public string Get(string language, string key)
    {
        if (_text.TryGetValue(language, out var translation) && translation.TryGetValue(key, out var value))
        {
            return value;
        }

        return _text[FALLBACK].TryGetValue(key, out var fallback)
            ? fallback
            : throw new KeyNotFoundException($"There is no text for '{key}'.");
    }

    /// <summary>The language of a visitor: the one they chose with <c>?lang=</c>, then the best their browser asks for.</summary>
    public string Resolve(HttpRequest request)
    {
        var chosen = request.Query["lang"].ToString().ToLowerInvariant();
        if (Languages.Contains(chosen))
        {
            return chosen;
        }

        foreach (var entry in request.GetTypedHeaders().AcceptLanguage.OrderByDescending(x => x.Quality ?? 1.0))
        {
            var primary = (entry.Value.Value ?? string.Empty).Split('-')[0].ToLowerInvariant();
            if (Languages.Contains(primary))
            {
                return primary;
            }
        }

        return FALLBACK;
    }
}
