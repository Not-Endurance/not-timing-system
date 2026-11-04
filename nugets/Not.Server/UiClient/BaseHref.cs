using System.Text.RegularExpressions;

namespace Not.Server.UiClient;

// Points the document's base element at the client's prefix, so the client's relative URLs resolve under it.
internal static class BaseHref
{
    static readonly Regex RootBase = new(
        @"<base\s+href=([""'])/\1\s*/?>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    public static string Rewrite(string html, string prefix)
    {
        return RootBase.Replace(html, $"<base href=\"{prefix}/\" />", 1);
    }
}
