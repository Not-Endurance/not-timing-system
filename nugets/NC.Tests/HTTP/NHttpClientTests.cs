using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NC.Application.HTTP;
using Xunit;

namespace NC.Tests.HTTP;

public sealed class NHttpClientTests
{
    const string BASE_URL = "http://test.local/api";

    [Fact]
    public async Task SendRawJson_WhenPostWithJson_ShouldSendTheBodyUnchangedAsJson()
    {
        var stub = new StubHandler(HttpStatusCode.Accepted, string.Empty);
        var client = CreateClient(stub);

        await client.SendRawJson(HttpMethod.Post, "sign-in/code", "{\"email\":\"ana@example.test\"}");

        Assert.Equal(HttpMethod.Post, stub.Request!.Method);
        Assert.EndsWith("/sign-in/code", stub.Request.RequestUri!.ToString());
        Assert.Equal("{\"email\":\"ana@example.test\"}", stub.RequestBody);
        Assert.Equal("application/json", stub.Request.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task SendRawJson_WhenRequestSucceeds_ShouldReturnTheBodyUnchanged()
    {
        var stub = new StubHandler(HttpStatusCode.OK, "{\"signedIn\":true,\"displayName\":\"Ana Ivanova\"}");
        var client = CreateClient(stub);

        var body = await client.SendRawJson(HttpMethod.Get, "me");

        Assert.Equal("{\"signedIn\":true,\"displayName\":\"Ana Ivanova\"}", body);
    }

    [Fact]
    public async Task SendRawJson_WhenStatusIsRefused_ShouldThrowWithTheStatusCode()
    {
        var stub = new StubHandler(HttpStatusCode.Unauthorized, "{\"error\":\"code-not-accepted\"}");
        var client = CreateClient(stub);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendRawJson(HttpMethod.Post, "sign-in/verify", "{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task SendRawJson_WhenNoJsonIsGiven_ShouldSendNoBody()
    {
        var stub = new StubHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(stub);

        await client.SendRawJson(HttpMethod.Post, "sign-out");

        Assert.Null(stub.Request!.Content);
    }

    static NHttpClient CreateClient(HttpMessageHandler handler)
    {
        var factory = new StubFactory(new HttpClient(handler));
        return new NHttpClient(factory, NullLogger<NHttpClient>.Instance, Options.Create(new NHttpSettings { Url = BASE_URL }));
    }

    sealed class StubFactory : IHttpClientFactory
    {
        readonly HttpClient _client;

        public StubFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    sealed class StubHandler : HttpMessageHandler
    {
        readonly HttpStatusCode _status;
        readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }
}
