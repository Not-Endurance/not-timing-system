using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Not.Application.HTTP;
using Not.Serialization.JSON;
using Not.Structures;
using NTS.Application.Watcher;
using NTS.Storage.REST;

namespace NTS.Judge.Tests.Core;

public class UserSessionRestApiRepositoryTests
{
    [Fact]
    public async Task Read_by_user_identifier_unwraps_session_from_result_envelope()
    {
        var expected = new NtsUserSessionModel { Id = 17, UserIdentifier = "entra-1" };
        expected.ReplaceState(new NtsUserSessionStateModel { EventId = 33 });

        var handler = new RecordingHttpMessageHandler
        {
            ResponseFactory = _ => CreateJsonResponse(Result.Success(expected)),
        };
        var repository = new UserSessionRestApiRepository(CreateClient(handler));

        var result = await repository.ReadByUserIdentifier("entra-1");

        Assert.NotNull(result);
        Assert.Equal(17, result!.Id);
        Assert.Equal("entra-1", result.UserIdentifier);
        Assert.Equal(33, result.State?.EventId);
    }

    static NHttpClient CreateClient(HttpMessageHandler handler)
    {
        return new NHttpClient(
            new TestHttpClientFactory(new HttpClient(handler)),
            NullLogger<NHttpClient>.Instance,
            Options.Create(new NHttpSettings { Url = "https://nexus.test/api" })
        );
    }

    static HttpResponseMessage CreateJsonResponse(object payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode) { Content = new StringContent(payload.ToJson()) };
    }

    sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(
                ResponseFactory?.Invoke(request)
                    ?? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(Result.Success().ToJson()),
                    }
            );
        }
    }

    sealed class TestHttpClientFactory : IHttpClientFactory
    {
        readonly HttpClient _client;

        public TestHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }
}
