using System.Globalization;

namespace NC.Server.UiClient;

internal sealed class PrecompressedVariant
{
    public PrecompressedVariant(string encoding, string extension)
    {
        Encoding = encoding;
        Extension = extension;
    }

    public string Encoding { get; }

    public string Extension { get; }
}

// Chooses which precompressed variant to send for a request, given the Accept-Encoding header.
internal static class PrecompressedEncoding
{
    // Brotli first, then gzip: the same variants the build produces.
    static readonly PrecompressedVariant[] Preferred = [new("br", ".br"), new("gzip", ".gz")];

    public static PrecompressedVariant? Select(string? acceptEncoding, Func<string, bool> hasVariant)
    {
        var accepted = Accepted(acceptEncoding);
        foreach (var variant in Preferred)
        {
            if (accepted.Contains(variant.Encoding) && hasVariant(variant.Extension))
            {
                return variant;
            }
        }

        return null;
    }

    static HashSet<string> Accepted(string? acceptEncoding)
    {
        var accepted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(acceptEncoding))
        {
            return accepted;
        }

        foreach (var entry in acceptEncoding.Split(','))
        {
            var parts = entry.Split(';');
            var encoding = parts[0].Trim();
            if (encoding.Length > 0 && !IsRefused(parts))
            {
                accepted.Add(encoding);
            }
        }

        return accepted;
    }

    static bool IsRefused(string[] parts)
    {
        for (var index = 1; index < parts.Length; index++)
        {
            var parameter = parts[index].Trim();
            if (
                parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(parameter[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var quality)
            )
            {
                return quality <= 0;
            }
        }

        return false;
    }
}
