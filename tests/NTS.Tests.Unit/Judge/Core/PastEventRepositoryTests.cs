using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Serialization.JSON;
using Not.Structures;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.PastEvents;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.Core.Repositories;

namespace NTS.Judge.Tests.Core;

public class PastEventRepositoryTests
{
    [Fact]
    public async Task ParticipationRepository_ReadForEvent_UsesRequestedPastEvent()
    {
        var handler = CreateHandler(Result.Success<IEnumerable<ParticipationModel>>([]));
        var repository = new PastParticipationRepository(CreateClient(handler));

        await repository.ReadForEvent(14);

        AssertRequest(handler, "https://nexus.test/api/events/14/participations");
    }

    [Fact]
    public async Task RankingRepository_ReadForEvent_UsesRequestedPastEvent()
    {
        var handler = CreateHandler(Result.Success<IEnumerable<RankingModel>>([]));
        var repository = new PastRankingRepository(CreateClient(handler));

        await repository.ReadForEvent(14);

        AssertRequest(handler, "https://nexus.test/api/events/14/rankings");
    }

    [Fact]
    public async Task OfficialRepository_ReadForEvent_UsesRequestedPastEvent()
    {
        var handler = CreateHandler(Result.Success<IEnumerable<OfficialModel>>([]));
        var repository = new PastOfficialRepository(CreateClient(handler));

        await repository.ReadForEvent(14);

        AssertRequest(handler, "https://nexus.test/api/events/14/officials");
    }

    [Fact]
    public void PastRepositoryContracts_DoNotExposeRepositoryMutationSurface()
    {
        Assert.False(InheritsRepository(typeof(IPastParticipationRepository), typeof(Participation)));
        Assert.False(InheritsRepository(typeof(IPastRankingRepository), typeof(Ranking)));
        Assert.False(InheritsRepository(typeof(IPastOfficialRepository), typeof(Official)));
    }

    static bool InheritsRepository(Type contract, Type entity)
    {
        return contract.GetInterfaces().Contains(typeof(IRepository<>).MakeGenericType(entity));
    }

    static NHttpClient CreateClient(HttpMessageHandler handler)
    {
        return new NHttpClient(
            new TestHttpClientFactory(new HttpClient(handler)),
            NullLogger<NHttpClient>.Instance,
            Options.Create(new NHttpSettings { Url = "https://nexus.test/api" })
        );
    }

    static RecordingHttpMessageHandler CreateHandler(object payload)
    {
        return new() { ResponseFactory = _ => CreateJsonResponse(payload) };
    }

    static void AssertRequest(RecordingHttpMessageHandler handler, string expectedUrl)
    {
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(expectedUrl, request.RequestUri?.ToString());
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
}
