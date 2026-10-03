using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// The sign-in, registration and passkey pages are static pages of the Api that the Ui links to (decided for #599,
/// #600 and #601). Their text is rendered into them on the server in the visitor's language; the files are embedded in
/// the assembly so a publish of the Ui cannot lose them.
/// </summary>
internal static class AccountPages
{
    // Escapes what HTML treats specially and leaves letters of every language as they are.
    static readonly HtmlEncoder ENCODER = HtmlEncoder.Create(UnicodeRanges.All);
    static readonly Dictionary<string, (string Resource, string ContentType)> ASSETS = new()
    {
        ["account.css"] = ("account/pages/account.css", "text/css; charset=utf-8"),
        ["sign-in.js"] = ("account/pages/sign-in.js", "text/javascript; charset=utf-8"),
    };

    public static IResult SignIn(HttpContext context, AccountText text)
    {
        var language = text.Resolve(context.Request);
        var values = new Dictionary<string, string>
        {
            ["lang"] = language,
            ["returnUrl"] = SafeReturnUrl(context.Request.Query["returnUrl"]),
        };
        var html = Regex.Replace(
            ReadText("account/pages/sign-in.html"),
            @"\{\{([A-Za-z0-9_.]+)\}\}",
            match =>
            {
                var key = match.Groups[1].Value;
                return ENCODER.Encode(values.TryGetValue(key, out var value) ? value : text.Get(language, key));
            }
        );

        // What the page says depends on the visitor and where they came from: nothing keeps a copy of it.
        context.Response.Headers.CacheControl = "no-store";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    public static IResult Asset(HttpContext context, string name)
    {
        if (!ASSETS.TryGetValue(name, out var asset))
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = "public, max-age=3600";
        return Results.Bytes(ReadBytes(asset.Resource), asset.ContentType);
    }

    /// <summary>Only a path of this site, so a sign-in cannot be used to send a visitor somewhere else.</summary>
    public static string SafeReturnUrl(string? value)
    {
        return
            !string.IsNullOrEmpty(value)
            && value.StartsWith('/')
            && !value.StartsWith("//", StringComparison.Ordinal)
            && !value.StartsWith("/\\", StringComparison.Ordinal)
            ? value
            : "/";
    }

    static string ReadText(string resource)
    {
        using var reader = new StreamReader(Open(resource));
        return reader.ReadToEnd();
    }

    static byte[] ReadBytes(string resource)
    {
        using var stream = Open(resource);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    static Stream Open(string resource)
    {
        return typeof(AccountPages).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"The page file '{resource}' is not embedded.");
    }
}
