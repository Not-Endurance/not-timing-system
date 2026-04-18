using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Not.Application.HTTP;
using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using Not.Notify;
using Not.Serialization.JSON;
using Not.Storage.REST;
using Not.Structures;
using NTS.Judge.Tests.Core.Implementations;

namespace NTS.Judge.Tests.Core;

public class RestApiRepositoryEnvelopeTests
{
    [Fact]
    public async Task Create_warns_when_result_envelope_contains_validation_errors()
    {
        var notifier = new TestNotifier();
        NotificationHelper.Configure(notifier);

        try
        {
            var handler = new RecordingHttpMessageHandler
            {
                ResponseFactory = _ => CreateJsonResponse(Result.Failure("Validation warning")),
            };
            var repository = new TestRestRepository(CreateClient(handler));

            await repository.Create(new TestEntity(7, "Test"));

            Assert.Equal(["Validation warning"], notifier.WarningMessages);
            Assert.Empty(notifier.Errors);
        }
        finally
        {
            NotificationHelper.Clear(notifier);
        }
    }

    [Fact]
    public async Task Create_reports_unhandled_http_errors_through_exception_notifications()
    {
        var notifier = new TestNotifier();
        NotificationHelper.Configure(notifier);

        try
        {
            var handler = new RecordingHttpMessageHandler
            {
                ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("boom"),
                },
            };
            var repository = new TestRestRepository(CreateClient(handler));

            await repository.Create(new TestEntity(7, "Test"));

            var exception = Assert.Single(notifier.Errors);
            Assert.IsType<HttpRequestException>(exception);
            Assert.Empty(notifier.WarningMessages);
        }
        finally
        {
            NotificationHelper.Clear(notifier);
        }
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
                    ?? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Result.Success().ToJson()) }
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

    sealed class TestRestRepository : RestApiRepository<TestEntity, TestModel>
    {
        public TestRestRepository(NHttpClient client)
            : base("tests", client) { }
    }

    sealed class TestEntity : IEntity
    {
        public TestEntity(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string Name { get; }
    }

    sealed class TestModel : IKrudModel<TestEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        public void MapFrom(TestEntity entity)
        {
            Id = entity.Id;
            Name = entity.Name;
        }

        public TestEntity MapToEntity()
        {
            return new TestEntity(Id, Name);
        }
    }
}
