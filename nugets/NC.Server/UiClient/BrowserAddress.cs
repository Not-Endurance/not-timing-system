namespace NC.Server.UiClient;

// Turns the addresses a server is listening on into a URL a browser can open. Wildcard hosts become localhost.
internal static class BrowserAddress
{
    static readonly string[] WildcardHosts = ["*", "+", "0.0.0.0", "[::]", "::"];

    public static string? From(IEnumerable<string> serverAddresses, string path)
    {
        foreach (var address in serverAddresses)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var host = WildcardHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) ? "localhost" : uri.Host;
            var root = new UriBuilder(uri.Scheme, host, uri.Port).Uri;
            return new Uri(root, path).AbsoluteUri;
        }

        return null;
    }
}
