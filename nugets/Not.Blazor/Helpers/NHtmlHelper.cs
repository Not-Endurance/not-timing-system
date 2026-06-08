using System.Net;
using System.Text.RegularExpressions;

namespace Not.Blazor.Helpers;

internal static class NHtmlHelper
{
    static readonly Regex ImagePathRegex = new(
        "(?<prefix><img\\b[^>]*?\\bsrc\\s*=\\s*[\"'])(?<path>[^\"']+)(?<suffix>[\"'][^>]*>)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    public static string ReplaceImagePaths(string html, Func<string, string?> replace)
    {
        return ImagePathRegex.Replace(
            html,
            match =>
            {
                var imagePath = WebUtility.HtmlDecode(match.Groups["path"].Value);
                var replacement = replace(imagePath);
                if (replacement == null || replacement == imagePath)
                {
                    return match.Value;
                }

                return $"{match.Groups["prefix"].Value}{WebUtility.HtmlEncode(replacement)}{match.Groups["suffix"].Value}";
            }
        );
    }
}
