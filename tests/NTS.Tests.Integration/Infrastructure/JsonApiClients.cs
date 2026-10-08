using Microsoft.Extensions.Options;
using Not.Application.HTTP;
using NTS.Contracts;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// A client of the Api as the Ui has one (#603): the JSON:API client with its settings and the session cookie of a person
/// signed in, reaching the host in this process, which is what the repositories of the Ui are built on. It records what it
/// asked for, so that a test can tell what was asked of the server and what was left to the client.
/// </summary>
internal static class JsonApiClients
{
    public static JsonApiClient Of(ApiFactory api, TenancySeed.Person? person, out Requests requests)
    {
        var cookie = new Cookie(
            person == null ? null : $"{ApiSessions.COOKIE_NAME}={person.Page.Cookie(ApiSessions.COOKIE_NAME)}"
        );
        return WithCookie(api, cookie, out requests);
    }

    /// <summary>A client whose cookie is the one given, which a test can give or take away after the client was made.</summary>
    public static JsonApiClient WithCookie(ApiFactory api, Cookie cookie, out Requests requests)
    {
        requests = new Requests();
        var http = api.CreateDefaultClient(new Uri("https://localhost"), cookie, requests);
        var settings = new JsonApiSettings { Url = "https://localhost/api" };
        settings.WriteHeaders[ApplicationConstants.WRITE_HEADER] = ApplicationConstants.WRITE_HEADER_VALUE;
        return new JsonApiClient(new OneClient(http), Options.Create(settings));
    }

    /// <summary>
    /// A client of an Api on a real port (the Kestrel host of a browser test) and not in this process, with the session cookie
    /// of the person, as a program outside the browser has one.
    /// </summary>
    public static JsonApiClient Over(Uri address, TenancySeed.Person person, out Requests requests)
    {
        requests = new Requests();
        var cookie = new Cookie($"{ApiSessions.COOKIE_NAME}={person.Page.Cookie(ApiSessions.COOKIE_NAME)}");
        var chain = new HttpClientHandler();
        cookie.InnerHandler = requests;
        requests.InnerHandler = chain;
        var http = new HttpClient(cookie);
        var settings = new JsonApiSettings { Url = $"{address.ToString().TrimEnd('/')}/api" };
        settings.WriteHeaders[ApplicationConstants.WRITE_HEADER] = ApplicationConstants.WRITE_HEADER_VALUE;
        return new JsonApiClient(new OneClient(http), Options.Create(settings));
    }

    /// <summary>What a client asked of the host, in order, as <c>METHOD /path?query</c>.</summary>
    internal sealed class Requests : DelegatingHandler
    {
        public List<string> Asked { get; } = [];

        /// <summary>Answers a request in the place of the host when it names an answer, so that a test can make one fail.</summary>
        public Func<HttpRequestMessage, HttpResponseMessage?>? Answer { get; set; }

        /// <summary>What a request waits for before it goes on, so that a test can hold one back and look at what happens meanwhile.</summary>
        public Func<HttpRequestMessage, Task>? Before { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            lock (Asked)
            {
                Asked.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
            }

            if (Before != null)
            {
                await Before(request);
            }

            return Answer?.Invoke(request) ?? await base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>The cookie a browser sends to the host, as <c>name=value</c>; none is a visitor.</summary>
    internal sealed class Cookie : DelegatingHandler
    {
        public Cookie(string? value)
        {
            Value = value;
        }

        public string? Value { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (Value != null)
            {
                request.Headers.Add("Cookie", Value);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    sealed class OneClient : IHttpClientFactory
    {
        readonly HttpClient _client;

        public OneClient(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }
}
