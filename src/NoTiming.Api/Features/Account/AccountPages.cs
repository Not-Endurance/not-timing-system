using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using Microsoft.AspNetCore.Antiforgery;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// The sign-in, registration and passkey pages are static pages of the Api that the Ui links to (decided for #599,
/// #600 and #601). Their text is rendered into them on the server in the visitor's language, together with the token
/// the ceremony requests send back; the files are embedded in the assembly so a publish of the Ui cannot lose them.
/// </summary>
internal static class AccountPages
{
    // Escapes what HTML treats specially and leaves letters of every language as they are.
    static readonly HtmlEncoder ENCODER = HtmlEncoder.Create(UnicodeRanges.All);
    static readonly ConcurrentDictionary<string, string> TEXTS = new();
    static readonly ConcurrentDictionary<string, byte[]> FILES = new();
    static readonly Dictionary<string, (string Resource, string ContentType)> ASSETS = new()
    {
        ["account.css"] = ("account/pages/account.css", "text/css; charset=utf-8"),
        ["sign-in.js"] = ("account/pages/sign-in.js", "text/javascript; charset=utf-8"),
        ["passkey-support.js"] = ("account/pages/passkey-support.js", "text/javascript; charset=utf-8"),
        ["passkeys.js"] = ("account/pages/passkeys.js", "text/javascript; charset=utf-8"),
    };

    public static IResult SignIn(
        HttpContext context,
        AccountText text,
        IAntiforgery antiforgery,
        PasskeyAvailability passkeys
    )
    {
        return Render(context, "account/pages/sign-in.html", text, antiforgery, passkeys, offer: false);
    }

    /// <summary>
    /// The passkeys of the signed-in person. Anyone else is sent to sign in and back. With <c>?offer=1</c> it opens
    /// as the offer made after a code sign-in: add a passkey, or go on.
    /// </summary>
    public static IResult Passkeys(
        HttpContext context,
        AccountText text,
        IAntiforgery antiforgery,
        PasskeyAvailability passkeys
    )
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            var back = context.Request.Path + context.Request.QueryString;
            return Results.Redirect("/sign-in?returnUrl=" + Uri.EscapeDataString(back));
        }

        return Render(
            context,
            "account/pages/passkeys.html",
            text,
            antiforgery,
            passkeys,
            offer: context.Request.Query["offer"] == "1"
        );
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

    /// <summary>
    /// Only a path of this site, so a sign-in cannot be used to send a visitor somewhere else. A control character
    /// anywhere is refused: a browser removes tabs and line breaks from a URL, so <c>/&lt;tab&gt;/host</c> would become
    /// <c>//host</c>.
    /// </summary>
    public static string SafeReturnUrl(string? value)
    {
        return
            !string.IsNullOrEmpty(value)
            && value.StartsWith('/')
            && !value.StartsWith("//", StringComparison.Ordinal)
            && !value.StartsWith("/\\", StringComparison.Ordinal)
            && !value.Any(char.IsControl)
            ? value
            : "/";
    }

    static IResult Render(
        HttpContext context,
        string resource,
        AccountText text,
        IAntiforgery antiforgery,
        PasskeyAvailability passkeys,
        bool offer
    )
    {
        var language = text.Resolve(context.Request);
        var values = new Dictionary<string, string>
        {
            ["lang"] = language,
            ["returnUrl"] = SafeReturnUrl(context.Request.Query["returnUrl"]),
            ["antiforgery"] = antiforgery.GetAndStoreTokens(context).RequestToken ?? string.Empty,
            ["passkeysEnabled"] = passkeys.Enabled ? "true" : "false",
            ["offer"] = offer ? "true" : "false",
        };
        var html = Regex.Replace(
            ReadText(resource),
            @"\{\{([A-Za-z0-9_.]+)\}\}",
            match =>
            {
                var key = match.Groups[1].Value;
                return ENCODER.Encode(values.TryGetValue(key, out var value) ? value : text.Get(language, key));
            }
        );

        // What the page says depends on the visitor and where they came from, and it carries their token: nothing
        // keeps a copy of it.
        context.Response.Headers.CacheControl = "no-store";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    /// <summary>The files are embedded and cannot change while the host runs, so each is read once.</summary>
    static string ReadText(string resource)
    {
        return TEXTS.GetOrAdd(
            resource,
            name =>
            {
                using var reader = new StreamReader(Open(name));
                return reader.ReadToEnd();
            }
        );
    }

    static byte[] ReadBytes(string resource)
    {
        return FILES.GetOrAdd(
            resource,
            name =>
            {
                using var stream = Open(name);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
        );
    }

    static Stream Open(string resource)
    {
        return typeof(AccountPages).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"The page file '{resource}' is not embedded.");
    }
}
