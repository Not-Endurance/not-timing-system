using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Not.Application.HTTP;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Storage.Core.Repositories;

namespace NTS.Judge.Tests.Core;

public class EnduranceEventRestRepositoryTests
{
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
            Options.Create(new NHttpSettings { Url = "https://nexus.test/api" }),
            new TestNotifier()
        );
    }

    static EnduranceEvent CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            new PopulatedPlace(country, "Sofia", "Sofia"),
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
            id
        );
    }

    sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") });
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

    sealed class TestNotifier : INotifier
    {
        public void Inform(string message) { }

        public void Success(string message) { }

        public void Warn(string message) { }

        public void Error(string message) { }

        public void Error(Exception ex) { }
    }

    sealed class TestSocketContext : INtsSocketContext
    {
        public bool IsConnected => Event != null;
        public Not.Application.RPC.SignalR.SocketConnectionStatus Status =>
            IsConnected
                ? Not.Application.RPC.SignalR.SocketConnectionStatus.Connected
                : Not.Application.RPC.SignalR.SocketConnectionStatus.Disconnected;
        public EnduranceEvent? Event { get; set; }
    }
}
