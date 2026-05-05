using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Not.Application.Authentication.User;
using Not.Application.HTTP;
using Not.Serialization.JSON;
using Not.Structures;
using NTS.Witness.Storage.Repositories;

namespace NTS.Authentication.Tests;

public class UserApiRepositoryTests
{
    [Fact]
    public async Task Get_unwraps_user_from_result_envelope()
    {
        var handler = new RecordingHttpMessageHandler
        {
            ResponseFactory = _ =>
                CreateJsonResponse(Result.Success(new NUserModel("user@example.com", id: 7) { Name = "Jane Doe" })),
        };
        var repository = CreateRepository(handler);

        var result = await repository.Get("user@example.com");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("user@example.com", result.Data!.Email);
        Assert.Equal("Jane Doe", result.Data.Name);
    }

    [Fact]
    public async Task Register_preserves_errors_from_result_envelope()
    {
        var handler = new RecordingHttpMessageHandler
        {
            ResponseFactory = _ => CreateJsonResponse(Result.Failure<NUserModel>("Email already exists")),
        };
        var repository = CreateRepository(handler);

        var result = await repository.Register(new NUserRegistration("user@example.com"));

        Assert.False(result.IsSuccess);
        Assert.Equal(["Email already exists"], result.Errors);
        Assert.Null(result.Data);
    }

    static UserApiRepository CreateRepository(HttpMessageHandler handler)
    {
        var client = new NHttpClient(
            new TestHttpClientFactory(new HttpClient(handler)),
            NullLogger<NHttpClient>.Instance,
            Options.Create(new NHttpSettings { Url = "https://nexus.test/api" })
        );

        return new UserApiRepository(client);
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
