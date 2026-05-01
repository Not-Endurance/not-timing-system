using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Serialization.JSON;
using Not.Structures;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Storage.Core.Repositories;

namespace NTS.Judge.Tests.Core;

public class EnduranceEventRestRepositoryTests
{
    [Fact]
    public async Task ReadActive_calls_active_endpoint_and_maps_events()
    {
        var models = new[]
        {
            EnduranceEventModel.From(CreateEvent(14)),
            EnduranceEventModel.From(CreateEvent(21)),
        };
        var handler = new RecordingHttpMessageHandler
        {
            ResponseFactory = _ => CreateJsonResponse(Result.Success<IEnumerable<EnduranceEventModel>>(models)),
        };
        var client = CreateClient(handler);
        var repository = new EnduranceEventRestRepository(client, new TestSocketContext());

        var result = (await repository.ReadActive()).ToList();

        Assert.Equal([14, 21], result.Select(x => x.Id));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://nexus.test/api/endurance-event/active", request.RequestUri?.ToString());
    }

    [Fact]
    public async Task ReadPast_calls_past_endpoint_and_maps_events()
    {
        var models = new[]
        {
            EnduranceEventModel.From(CreateEvent(31)),
            EnduranceEventModel.From(CreateEvent(32)),
        };
        var handler = new RecordingHttpMessageHandler
        {
            ResponseFactory = _ => CreateJsonResponse(Result.Success<IEnumerable<EnduranceEventModel>>(models)),
        };
        var client = CreateClient(handler);
        var repository = new EnduranceEventRestRepository(client, new TestSocketContext());

        var result = (await repository.ReadPast()).ToList();

        Assert.Equal([31, 32], result.Select(x => x.Id));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://nexus.test/api/endurance-event/past", request.RequestUri?.ToString());
    }

    [Fact]
    public async Task Start_returns_mapped_endurance_event_when_response_contains_success_result()
    {
        var model = EnduranceEventModel.From(CreateEvent(14));
        var handler = new RecordingHttpMessageHandler
        {
            ResponseFactory = _ => CreateJsonResponse(Result.Success(model)),
        };
        var client = CreateClient(handler);
        var repository = new EnduranceEventRestRepository(client, new TestSocketContext());

        var result = await repository.Start(14);

        Assert.Equal(14, result.Id);
        Assert.Equal("Sofia", result.Name);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://nexus.test/api/endurance-event/14/start", request.RequestUri?.ToString());
    }

    [Fact]
    public async Task Start_throws_domain_exception_when_response_contains_errors()
    {
        var handler = new RecordingHttpMessageHandler
        {
            ResponseFactory = _ => CreateJsonResponse(Result.Failure<EnduranceEventModel>("Start blocked")),
        };
        var client = CreateClient(handler);
        var repository = new EnduranceEventRestRepository(client, new TestSocketContext());

        var exception = await Assert.ThrowsAsync<DomainException>(() => repository.Start(14));

        Assert.Equal("Start blocked", exception.Message);
    }

    [Fact]
    public async Task Reset_calls_endurance_event_reset_endpoint_for_current_event()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = CreateClient(handler);
        var repository = new EnduranceEventRestRepository(client, new TestSocketContext { Event = CreateEvent(14) });

        await repository.Reset();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("https://nexus.test/api/endurance-event/14/reset", request.RequestUri?.ToString());
    }

    [Fact]
    public async Task Reset_does_nothing_when_no_event_is_selected()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = CreateClient(handler);
        var repository = new EnduranceEventRestRepository(client, new TestSocketContext());

        await repository.Reset();

        Assert.Empty(handler.Requests);
    }

    static NHttpClient CreateClient(HttpMessageHandler handler)
    {
        return new NHttpClient(
            new TestHttpClientFactory(new HttpClient(handler)),
            NullLogger<NHttpClient>.Instance,
            Options.Create(new NHttpSettings { Url = "https://nexus.test/api" })
        );
    }

    static EnduranceEvent CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            country,
            "Sofia",
            "Sofia",
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
            id
        );
    }

    static HttpResponseMessage CreateJsonResponse(object payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode) { Content = new StringContent(payload.ToJson()) };
    }

    sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(request);
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

    sealed class TestSocketContext : INtsSocketContext
    {
        public bool IsConnected => Event != null;
        public Not.Application.RPC.SocketConnectionStatus Status =>
            IsConnected
                ? Not.Application.RPC.SocketConnectionStatus.Connected
                : Not.Application.RPC.SocketConnectionStatus.Disconnected;
        public EnduranceEvent? Event { get; set; }
    }
}
