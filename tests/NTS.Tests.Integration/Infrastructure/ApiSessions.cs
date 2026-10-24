using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Not.Identity.Email;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// Signing in the way a person does (ADR-0002): ask for a code, read it from the outbox the test host keeps instead of
/// an inbox, send it back, and carry the cookie the host sets. There is no other way in: the host has no sign-in as and
/// no test scheme. The cookie is carried by hand so it works for an in-memory client and for one on a real loopback
/// port alike, where the Secure cookie of a plain-http address would not be sent back.
/// </summary>
internal static class ApiSessions
{
    public const string COOKIE_NAME = "__Host-NoTiming";
    public const string MEDIA_TYPE = "application/vnd.api+json";

    public static Task<HttpResponseMessage> RequestCodeAsync(
        HttpClient client,
        string email,
        string? language = null,
        string? from = null
    )
    {
        return PostAsync(client, "/api/code-challenges", "code-challenges", new { email }, language, from);
    }

    public static Task<HttpResponseMessage> CreateSessionAsync(
        HttpClient client,
        string email,
        string code,
        string? from = null
    )
    {
        return PostAsync(client, "/api/sessions", "sessions", new { email, code }, from: from);
    }

    /// <param name="from">The address the request comes from, for the host to see as the client's (see <see cref="ApiFactory"/>).</param>
    public static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        string type,
        object attributes,
        string? language = null,
        string? from = null
    )
    {
        var body = JsonSerializer.Serialize(new { data = new { type, attributes } });
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, MEDIA_TYPE),
        };
        if (language != null)
        {
            request.Headers.AcceptLanguage.ParseAdd(language);
        }

        if (from != null)
        {
            request.Headers.Add(ApiFactory.CLIENT_ADDRESS_HEADER, from);
        }

        return await client.SendAsync(request);
    }

    /// <summary>The code of the newest mail to the address, read from the outbox.</summary>
    public static string CodeSentTo(ApiFactory api, string email)
    {
        var message = api.Services.GetRequiredService<IEmailOutbox>().Messages.LastOrDefault(x => x.To == email);
        Assert.NotNull(message);
        var match = Regex.Match(message.TextBody, @"\b(\d{6})\b");
        Assert.True(match.Success, "The mail carries no six-digit code: " + message.TextBody);
        return match.Groups[1].Value;
    }

    public static async Task<SessionCookie> SignInAsync(ApiFactory api, HttpClient client, string email)
    {
        var requested = await RequestCodeAsync(client, email);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        var created = await CreateSessionAsync(client, email, CodeSentTo(api, email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return SessionCookie.From(created) ?? throw new InvalidOperationException("The host set no session cookie.");
    }

    public static HttpRequestMessage Request(HttpMethod method, string path, SessionCookie? cookie = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (cookie != null)
        {
            request.Headers.Add("Cookie", cookie.Header);
        }

        return request;
    }

    public static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path, SessionCookie? cookie)
    {
        using var request = Request(HttpMethod.Get, path, cookie);
        return await client.SendAsync(request);
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}

/// <summary>The session cookie a host set, as a browser would hold it.</summary>
internal sealed class SessionCookie
{
    public static SessionCookie? From(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var setCookie = values.FirstOrDefault(x =>
            x.StartsWith(ApiSessions.COOKIE_NAME + "=", StringComparison.Ordinal)
        );
        if (setCookie == null)
        {
            return null;
        }

        var parts = setCookie.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var pair = parts[0].Split('=', 2);
        return new SessionCookie(pair[0], pair[1], [.. parts.Skip(1).Select(x => x.ToLowerInvariant())], setCookie);
    }

    SessionCookie(string name, string value, IReadOnlyList<string> attributes, string setCookie)
    {
        Name = name;
        Value = value;
        Attributes = attributes;
        SetCookie = setCookie;
    }

    public string Name { get; }
    public string Value { get; }

    /// <summary>The attributes after the value, lower case: <c>httponly</c>, <c>samesite=lax</c>, <c>path=/</c>.</summary>
    public IReadOnlyList<string> Attributes { get; }

    public string SetCookie { get; }
    public string Header => $"{Name}={Value}";

    /// <summary>
    /// The cookie as a container for an <c>HttpClientHandler</c> or the <c>Cookies</c> option of a SignalR connection,
    /// for the address of a host on a real port. It is not marked Secure here: that address is plain http.
    /// </summary>
    public CookieContainer ToContainer(Uri address)
    {
        var container = new CookieContainer();
        container.Add(address, new Cookie(Name, Value));
        return container;
    }

    public DateTimeOffset? Expires()
    {
        var expires = Attributes.FirstOrDefault(x => x.StartsWith("expires=", StringComparison.Ordinal));
        return expires != null && DateTimeOffset.TryParse(expires["expires=".Length..], out var at) ? at : null;
    }
}
