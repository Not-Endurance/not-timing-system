namespace Not.Application.HTTP;

public static class HttpHelper
{
    public static string NormalizeUri(string? uri)
    {
        if (uri == null)
        {
            return "";
        }
        if (uri.StartsWith('/'))
        {
            return string.Join("", uri.Skip(1));
        }
        if (uri.EndsWith('/'))
        {
            return string.Join("", uri.SkipLast(1));
        }
        return uri;
    }

    public static string AddQueryString(string endpoint, IReadOnlyDictionary<string, string>? queryParameters)
    {
        if (queryParameters == null || queryParameters.Count == 0)
        {
            return endpoint;
        }

        var separator = endpoint.Contains('?') ? "&" : "?";
        var values = queryParameters.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}");
        return $"{endpoint}{separator}{string.Join("&", values)}";
    }
}
