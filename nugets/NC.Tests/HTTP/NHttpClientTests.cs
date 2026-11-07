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
    public async Task PostJson_WhenJsonIsGiven_ShouldSendTheBodyUnchangedAsJson()
    {
        var stub = new StubHandler(HttpStatusCode.Accepted, string.Empty);
        var client = CreateClient(stub);

        await client.PostJson("sign-in/code", "{\"email\":\"ana@example.test\"}");

        Assert.Equal(HttpMethod.Post, stub.Request!.Method);
        Assert.EndsWith("/sign-in/code", stub.Request.RequestUri!.ToString());
        Assert.Equal("{\"email\":\"ana@example.test\"}", stub.RequestBody);
        Assert.Equal("application/json", stub.Request.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task PostJson_WhenNoJsonIsGiven_ShouldSendNoBody()
    {
        var stub = new StubHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(stub);

        await client.PostJson("sign-out");

        Assert.Null(stub.Request!.Content);
    }

    [Fact]
    public async Task GetJson_WhenRequestSucceeds_ShouldReturnTheBodyInASuccessfulResult()
    {
        var stub = new StubHandler(HttpStatusCode.OK, "{\"signedIn\":true,\"displayName\":\"Ana Ivanova\"}");
        var client = CreateClient(stub);

        var result = await client.GetJson("me");

        Assert.Equal(HttpMethod.Get, stub.Request!.Method);
        Assert.True(result.IsSuccess);
        Assert.Equal("{\"signedIn\":true,\"displayName\":\"Ana Ivanova\"}", result.Data);
    }

    [Fact]
    public async Task GetJson_WhenStatusIsRefused_ShouldThrowWithTheStatusCode()
    {
        var stub = new StubHandler(HttpStatusCode.Unauthorized, "{\"error\":\"not-signed-in\"}");
        var client = CreateClient(stub);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetJson("me/profile"));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task PatchJson_ShouldSendPatchWithTheBodyAsJson()
    {
        var stub = new StubHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(stub);

        var result = await client.PatchJson("me/passkeys/1", "{\"name\":\"Laptop\"}");

        Assert.Equal(HttpMethod.Patch, stub.Request!.Method);
        Assert.EndsWith("/me/passkeys/1", stub.Request.RequestUri!.ToString());
        Assert.Equal("{\"name\":\"Laptop\"}", stub.RequestBody);
        Assert.Equal("application/json", stub.Request.Content!.Headers.ContentType!.MediaType);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteJson_ShouldSendDeleteWithNoBody()
    {
        var stub = new StubHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(stub);

        var result = await client.DeleteJson("me/passkeys/1");

        Assert.Equal(HttpMethod.Delete, stub.Request!.Method);
        Assert.Null(stub.Request.Content);
        Assert.True(result.IsSuccess);
    }

    static NHttpClient CreateClient(HttpMessageHandler handler)
    {
        var factory = new StubFactory(new HttpClient(handler));
        return new NHttpClient(
            factory,
            NullLogger<NHttpClient>.Instance,
            Options.Create(new NHttpSettings { Url = BASE_URL })
        );
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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }
}
