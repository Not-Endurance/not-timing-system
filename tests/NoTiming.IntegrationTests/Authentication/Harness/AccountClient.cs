using System.Net.Http.Json;

namespace NoTiming.IntegrationTests.Authentication.Harness;

/// <summary>Talks to the sign-in routes over HTTP, the way the browser does: same origin, custom request header.</summary>
internal sealed class AccountClient : IDisposable
{
    const string REQUEST_HEADER = "X-NTS-Request";
    const string SESSION_COOKIE = "__Host-session";

    /// <summary>The session cookie as the browser sends it back: name and value only.</summary>
    public static string SessionCookieOf(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(SESSION_COOKIE + "=", StringComparison.Ordinal));
        return setCookie.Split(';')[0];
    }

    /// <summary>The Set-Cookie header of the session cookie, for checking its attributes.</summary>
    public static string SessionSetCookieOf(HttpResponseMessage response)
    {
        return response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(SESSION_COOKIE + "=", StringComparison.Ordinal));
    }

    readonly HttpClient _http;
    readonly string _origin;

    public AccountClient(Uri baseUrl, int port)
    {
        _http = new HttpClient { BaseAddress = baseUrl };
        _origin = $"http://app.localhost:{port}";
    }

    public Task<HttpResponseMessage> RequestSignInCodeAsync(string email)
    {
        return SendAsync(HttpMethod.Post, "api/sign-in/code", new { email });
    }

    public Task<HttpResponseMessage> RegisterAsync(
        string email,
        string givenName = "Ana",
        string surname = "Ivanova",
        string country = "BG",
        string? website = null
    )
    {
        return SendAsync(HttpMethod.Post, "api/register", new { email, givenName, surname, countryRegion = country, website });
    }

    public Task<HttpResponseMessage> VerifyAsync(string email, string? code)
    {
        return SendAsync(HttpMethod.Post, "api/sign-in/verify", new { email, code });
    }

    public Task<HttpResponseMessage> SignOutAsync(string cookie, bool crossSite = false)
    {
        return SendAsync(HttpMethod.Post, "api/sign-out", body: null, cookie, crossSite);
    }

    public Task<HttpResponseMessage> UpdateProfileAsync(string cookie, object body)
    {
        return SendAsync(new HttpMethod("PATCH"), "api/me", body, cookie);
    }

    public Task<HttpResponseMessage> MeAsync(string? cookie = null)
    {
        return SendAsync(HttpMethod.Get, "api/me", body: null, cookie);
    }

    public Task<HttpResponseMessage> PasskeySignInOptionsAsync()
    {
        return SendAsync(HttpMethod.Post, "api/sign-in/passkey/options", body: null);
    }

    public Task<HttpResponseMessage> PasskeySignInVerifyAsync(object credential)
    {
        return SendAsync(HttpMethod.Post, "api/sign-in/passkey/verify", credential);
    }

    public Task<HttpResponseMessage> PasskeyEnrolOptionsAsync(string cookie)
    {
        return SendAsync(HttpMethod.Post, "api/me/passkey/options", body: null, cookie);
    }

    public Task<HttpResponseMessage> ListPasskeysAsync(string cookie)
    {
        return SendAsync(HttpMethod.Get, "api/me/passkeys", body: null, cookie);
    }

    public Task<HttpResponseMessage> RenamePasskeyAsync(string cookie, string id, string name)
    {
        return SendAsync(new HttpMethod("PATCH"), $"api/me/passkeys/{id}", new { name }, cookie);
    }

    public Task<HttpResponseMessage> RemovePasskeyAsync(string cookie, string id)
    {
        return SendAsync(HttpMethod.Delete, $"api/me/passkeys/{id}", body: null, cookie);
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        string? cookie = null,
        bool crossSite = false
    )
    {
        using var request = new HttpRequestMessage(method, path);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("Origin", _origin);
            request.Headers.Add(REQUEST_HEADER, "1");
        }

        if (crossSite)
        {
            request.Headers.Add("Sec-Fetch-Site", "cross-site");
        }

        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await _http.SendAsync(request);
    }
}
