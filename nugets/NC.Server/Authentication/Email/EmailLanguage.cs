namespace NC.Server.Authentication.Email;

/// <summary>
/// Picks the language of a code email or a passkey notice from the Accept-Language header. The languages are the ones the
/// host ships resources for. English is the fallback.
/// </summary>
internal sealed class EmailLanguage
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
            var match = Match(pieces[0].Trim());
            if (match is null)
            {
                continue;
            }

            var quality = ParseQuality(pieces);
            if (quality > bestQuality)
            {
                best = match;
                bestQuality = quality;
            }
        }

        return best ?? English;
    }

    /// <summary>
    /// The packaged language for one requested tag. The whole tag is tried first, then each shorter form of it, so
    /// <c>zh-Hans-CN</c> finds <c>zh-Hans</c>. A tag never matches a different script or region of the same language.
    /// </summary>
    string? Match(string tag)
    {
        var candidate = tag;
        while (candidate.Length > 0)
        {
            var found = _supported.FirstOrDefault(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                return found;
            }

            var cut = candidate.LastIndexOf('-');
            if (cut < 0)
            {
                break;
            }

            candidate = candidate[..cut];
        }

        return null;
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
