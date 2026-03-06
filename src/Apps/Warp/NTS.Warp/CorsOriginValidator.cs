using System.Text.RegularExpressions;

namespace NTS.Warp;

internal sealed class CorsOriginValidator
{
    readonly HashSet<string> _allowedOrigins;
    readonly Regex[] _allowedOriginPatterns;

    public CorsOriginValidator(CorsSettings settings)
    {
        _allowedOrigins = (settings.AllowedOrigins ?? [])
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(NormalizeOrigin)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allowedOriginPatterns = (settings.AllowedOriginHostPatterns ?? [])
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => new Regex(
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant
            ))
            .ToArray();
    }

    public bool IsAllowed(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var normalizedOrigin = NormalizeOrigin(uri);
        if (_allowedOrigins.Contains(normalizedOrigin))
        {
            return true;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return _allowedOriginPatterns.Any(pattern => pattern.IsMatch(uri.Host));
    }

    static string NormalizeOrigin(string origin)
    {
        var trimmedOrigin = origin.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmedOrigin, UriKind.Absolute, out var uri))
        {
            return trimmedOrigin;
        }

        return NormalizeOrigin(uri);
    }

    static string NormalizeOrigin(Uri uri)
    {
        return $"{uri.Scheme}://{uri.Authority}";
    }
}
