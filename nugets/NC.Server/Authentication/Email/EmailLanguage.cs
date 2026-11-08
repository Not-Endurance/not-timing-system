namespace NC.Server.Authentication.Email;

/// <summary>
/// Picks the language of a code email or a passkey notice from the Accept-Language header. The languages are the ones the
/// host ships resources for. English is the fallback.
/// </summary>
public sealed class EmailLanguage
{
    public const string English = "en";

    readonly string[] _supported;

    public EmailLanguage(IEnumerable<string> supported)
    {
        _supported = [.. supported];
    }

    public string Choose(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage))
        {
            return English;
        }

        string? best = null;
        var bestQuality = 0.0;
        foreach (var part in acceptLanguage.Split(','))
        {
            var pieces = part.Trim().Split(';');
            var tag = pieces[0].Trim().Split('-')[0].ToLowerInvariant();
            if (!_supported.Contains(tag))
            {
                continue;
            }

            var quality = ParseQuality(pieces);
            if (quality > bestQuality)
            {
                best = tag;
                bestQuality = quality;
            }
        }

        return best ?? English;
    }

    static double ParseQuality(string[] pieces)
    {
        foreach (var piece in pieces.Skip(1))
        {
            var parts = piece.Trim().Split('=');
            if (
                parts.Length == 2
                && parts[0].Trim() == "q"
                && double.TryParse(
                    parts[1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var quality
                )
            )
            {
                return quality;
            }
        }

        return 1.0;
    }
}
