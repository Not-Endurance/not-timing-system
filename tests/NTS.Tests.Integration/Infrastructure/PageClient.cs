using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// A browser's part in a request, by hand: the cookies it has been given and sends back, and the antiforgery token the
/// pages of the Api carry for the ceremonies. A cookie the host clears is dropped, as a browser would.
/// </summary>
internal sealed class PageClient
{
    readonly HttpClient _http;
    readonly Dictionary<string, string> _cookies = [];

    public PageClient(HttpClient http)
    {
        _http = http;
    }

    public string? AntiforgeryToken { get; private set; }

    public static string? AntiforgeryTokenOf(string html)
    {
        var match = Regex.Match(html, "data-antiforgery=\"([^\"]*)\"");
        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    public string? Cookie(string name)
    {
        return _cookies.TryGetValue(name, out var value) ? value : null;
    }

    public void Set(SessionCookie cookie)
    {
        _cookies[cookie.Name] = cookie.Value;
    }

    /// <summary>Opens a page: keeps the cookies it sets and the antiforgery token it carries.</summary>
    public async Task<string> OpenAsync(string path, string? language = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (language != null)
        {
            request.Headers.AcceptLanguage.ParseAdd(language);
        }

        using var response = await SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();
        AntiforgeryToken = AntiforgeryTokenOf(html) ?? AntiforgeryToken;
        return html;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        if (_cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(x => $"{x.Key}={x.Value}")));
        }

        var response = await _http.SendAsync(request);
        Absorb(response);
        return response;
    }

    public Task<HttpResponseMessage> GetAsync(string path)
    {
        return SendAsync(new HttpRequestMessage(HttpMethod.Get, path));
    }

    public Task<HttpResponseMessage> DeleteAsync(string path)
    {
        return SendAsync(new HttpRequestMessage(HttpMethod.Delete, path));
    }

    /// <summary>A write the way the pages make it: the media type, and the token when it is asked for.</summary>
    public Task<HttpResponseMessage> WriteAsync(
        HttpMethod method,
        string path,
        string type,
        object attributes,
        bool withToken = true,
        string? language = null
    )
    {
        var body = JsonSerializer.Serialize(new { data = new { type, attributes } });
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(body, Encoding.UTF8, ApiSessions.MEDIA_TYPE),
        };
        if (withToken && AntiforgeryToken != null)
        {
            request.Headers.Add("X-XSRF-TOKEN", AntiforgeryToken);
        }

        if (language != null)
        {
            request.Headers.AcceptLanguage.ParseAdd(language);
        }

        return SendAsync(request);
    }

    /// <summary>A POST with no body, as the ceremony options are asked for.</summary>
    public Task<HttpResponseMessage> PostAsync(string path, bool withToken = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (withToken && AntiforgeryToken != null)
        {
            request.Headers.Add("X-XSRF-TOKEN", AntiforgeryToken);
        }

        return SendAsync(request);
    }

    void Absorb(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        foreach (var header in values)
        {
            var parts = header.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var pair = parts[0].Split('=', 2);
            var expires = parts
                .Skip(1)
                .FirstOrDefault(x => x.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
            var expired =
                expires != null
                && DateTimeOffset.TryParse(expires["expires=".Length..], out var at)
                && at < DateTimeOffset.UtcNow;
            if (pair.Length < 2 || string.IsNullOrEmpty(pair[1]) || expired)
            {
                _cookies.Remove(pair[0]);
            }
            else
            {
                _cookies[pair[0]] = pair[1];
            }
        }
    }
}
