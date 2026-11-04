namespace NoTiming.Host.Cors;

public static class CorsOriginMatcher
{
    public static bool IsSameHost(string? origin, string requestHost)
    {
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(requestHost))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var originHost = uri.Host.Trim('[', ']');
        return string.Equals(originHost, requestHost, StringComparison.OrdinalIgnoreCase);
    }
}
