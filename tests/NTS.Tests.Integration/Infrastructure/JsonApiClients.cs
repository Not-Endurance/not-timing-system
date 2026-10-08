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

    /// <summary>What a client asked of the host, in order, as <c>METHOD /path?query</c>.</summary>
    internal sealed class Requests : DelegatingHandler
    {
        public List<string> Asked { get; } = [];

        /// <summary>Answers a request in the place of the host when it names an answer, so that a test can make one fail.</summary>
        public Func<HttpRequestMessage, HttpResponseMessage?>? Answer { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Asked.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
            var answer = Answer?.Invoke(request);
            return answer != null ? Task.FromResult(answer) : base.SendAsync(request, cancellationToken);
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
