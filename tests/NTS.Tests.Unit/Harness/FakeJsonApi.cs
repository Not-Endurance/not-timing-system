using System.Net;

namespace NTS.Tests.Unit.Harness;

/// <summary>
/// The transport under <c>JsonApiClient</c> (ADR-0017): it answers each request from a script and records what the client
/// sent, so that a test checks the requests the client made and not the code that made them.
/// </summary>
public sealed class FakeJsonApi : HttpMessageHandler
{
    readonly Func<HttpRequestMessage, HttpResponseMessage> _answer;

    public FakeJsonApi(Func<HttpRequestMessage, HttpResponseMessage>? answer = null)
    {
        _answer = answer ?? (_ => new HttpResponseMessage(HttpStatusCode.OK));
    }

    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(x => x.Key, x => string.Join(", ", x.Value));
        Requests.Add(
            new RecordedRequest(
                request.Method,
                Uri.UnescapeDataString(request.RequestUri!.PathAndQuery),
                request.Headers.Accept.ToString(),
                headers,
                body
            )
        );
        return _answer(request);
    }
}

/// <summary>One request the client sent: its method, its path and query as they were asked for, its Accept header, its headers and its body.</summary>
public sealed class RecordedRequest
{
    public RecordedRequest(
        HttpMethod method,
        string url,
        string accept,
        IReadOnlyDictionary<string, string> headers,
        string? body
    )
    {
        Method = method;
        Url = url;
        Accept = accept;
        Headers = headers;
        Body = body;
    }

    public HttpMethod Method { get; }
    public string Url { get; }
    public string Accept { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
    public string? Body { get; }
}

/// <summary>The factory a client gets its <c>HttpClient</c> from, over the fake transport.</summary>
public sealed class FakeHttpClientFactory : IHttpClientFactory
{
    readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(_handler, disposeHandler: false);
    }
}
