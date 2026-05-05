using System.Net;
using System.Linq.Expressions;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Not.Application.Authentication.Abstractions;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Application.RPC;
using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using Not.Serialization.JSON;
using Not.Structures;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Socket;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Application.Core;
using NTS.Application.Settings;
using NTS.Application.Setup;
using NTS.Application.UserSession;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;
using NTS.Storage;
using NTS.Storage.Core.Repositories;
using NTS.Storage.REST;
using Not.Storage.Mongo;

namespace NTS.Judge.Tests.Core;

public class PastEventRepositoryTests
{
    [Fact]
    public async Task ParticipationRepository_FilteredReadMany_UsesRequestedPastEvent()
    {
        var eventId = 14;
        var handler = CreateHandler(Result.Success<IEnumerable<ParticipationModel>>([]));
        var repository = new ParticipationApiRepository(CreateClient(handler));

        await repository.ReadMany(x => x.EventId == eventId);

        AssertRequest(handler, "https://nexus.test/api/participations?%24filter=EventId%20eq%2014");
    }

    [Fact]
    public async Task RankingRepository_FilteredReadMany_UsesRequestedPastEvent()
    {
        var eventId = 14;
        var handler = CreateHandler(Result.Success<IEnumerable<RankingModel>>([]));
        var repository = new RankingApiRepository(CreateClient(handler));

        await repository.ReadMany(x => x.EventId == eventId);

        AssertRequest(handler, "https://nexus.test/api/rankings?%24filter=EventId%20eq%2014");
    }

    [Fact]
    public async Task OfficialRepository_FilteredReadMany_UsesRequestedPastEvent()
    {
        var eventId = 14;
        var handler = CreateHandler(Result.Success<IEnumerable<OfficialModel>>([]));
        var repository = new OfficialApiRepository(CreateClient(handler));

        await repository.ReadMany(x => x.EventId == eventId);

        AssertRequest(handler, "https://nexus.test/api/officials?%24filter=EventId%20eq%2014");
    }

    [Fact]
    public async Task EventScopedParticipationRepository_ReadMany_UsesSelectedEventQuery()
    {
        var handler = CreateHandler(Result.Success<IEnumerable<ParticipationModel>>([]));
        var socketContext = new TestSocketContext { Event = CreateEvent(14) };
        var repository = new ParticipationEventScopedApiRepository(
            CreateClient(handler),
            new EventScopeFactory<Participation>(socketContext)
        );

        await repository.ReadMany();

        AssertRequest(handler, "https://nexus.test/api/participations?%24filter=EventId%20eq%2014");
    }

    [Fact]
    public async Task EventScopedRepository_FilteredReadMany_CombinesSelectedEventAndReadFilter()
    {
        var handler = CreateHandler(Result.Success<IEnumerable<QueryEventModel>>([]));
        var repository = new QueryEventRepository(
            CreateClient(handler),
            new TestSocketContext { Event = CreateEvent(14) }
        );

        await repository.ReadMany(x => x.Id == 7);

        AssertRequest(
            handler,
            "https://nexus.test/api/query-documents?%24filter=%28EventId%20eq%2014%29%20and%20%28Id%20eq%207%29"
        );
    }

    [Fact]
    public async Task EventScopedRepository_Create_UsesSelectedEventQueryWithoutMutatingPayload()
    {
        var handler = CreateHandler(Result.Success(new QueryEventModel()));
        var repository = new QueryEventRepository(
            CreateClient(handler),
            new TestSocketContext { Event = CreateEvent(14) }
        );

        await repository.Create(new QueryEntity(1, 99));

        AssertRequest(
            handler,
            HttpMethod.Post,
            "https://nexus.test/api/query-documents?%24filter=EventId%20eq%2014"
        );
        var body = Assert.Single(handler.Bodies);
        Assert.Equal(99, body!.FromJson<QueryEventModel>().EventId);
    }

    [Fact]
    public void ConfigureNtsStorage_SeparatesCoreRepositoryContracts()
    {
        var services = new ServiceCollection();

        services.ConfigureNtsStorage(CreateConfiguration()).AddRestApiStorage();

        AssertRegistration<IEnduranceEventRepository, EnduranceEventApiRepository>(services);
        AssertRegistration<ISettingRepository, SettingApiRepository>(services);
        AssertRegistration<INtsUserSessionRepository, UserSessionApiRepository>(services);
        AssertRegistration<INUserSessionRepository<NtsUserSessionStateModel>, UserSessionApiRepository>(services);
        AssertRegistration<IRepository<Participation>, ParticipationApiRepository>(services);
        AssertRegistration<IRepository<Ranking>, RankingApiRepository>(services);
        AssertRegistration<IRepository<Official>, OfficialApiRepository>(services);
        AssertRegistration<IRepository<Country>, CountryApiRepository>(services);
        AssertRegistration<IRepository<Club>, ClubApiRepository>(services);
        AssertRegistration<IRepository<Horse>, HorseApiRepository>(services);
        AssertRegistration<IRepository<Athlete>, AthleteApiRepository>(services);
        AssertRegistration<IRepository<UpcomingEvent>, UpcomingEventApiRepository>(services);
        AssertRegistration<IUserEmailLookup, UserApiRepository>(services);
        AssertRegistration<IEventScopedRepository<Participation>, ParticipationEventScopedApiRepository>(services);
        AssertRegistration<IEventScopedRepository<Ranking>, RankingEventScopedApiRepository>(services);
        AssertRegistration<IEventScopedRepository<Official>, OfficialEventScopedApiRepository>(services);
        AssertRegistration<IEventScopedRepository<Handout>, HandoutEventScopedApiRepository>(services);
        AssertRegistration<IEventScopedRepository<SnapshotResult>, SnapshotResultEventScopedApiRepository>(services);

        AssertNoRegistration<IRepository<Participation>, ParticipationEventScopedApiRepository>(services);
        AssertNoRegistration<IRepository<Ranking>, RankingEventScopedApiRepository>(services);
        AssertNoRegistration<IRepository<Official>, OfficialEventScopedApiRepository>(services);
    }

    [Fact]
    public async Task CrudFunctions_ReadMany_AppliesQueryFilters()
    {
        var repository = new RecordingRepository<QueryDocument>();
        repository.Items.Add(new QueryDocument { Id = 1, EventId = 14 });
        repository.Items.Add(new QueryDocument { Id = 2, EventId = 99 });
        var functions = new TestCrudFunctions<QueryDocument>(repository);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?$filter=EventId%20eq%2014");

        var items = AssertOkPayload<IEnumerable<QueryDocument>>(await functions.ReadMany(context.Request));

        var item = Assert.Single(items);
        Assert.Equal(1, item.Id);
        Assert.Equal("EventId eq 14", repository.LastReadManyOptions?.Filter?.RawValue);
    }

    [Fact]
    public async Task CrudFunctions_Read_UsesRouteId()
    {
        var repository = new RecordingRepository<QueryDocument>();
        repository.Items.Add(new QueryDocument { Id = 7, EventId = 14 });
        repository.Items.Add(new QueryDocument { Id = 7, EventId = 99 });
        var functions = new TestCrudFunctions<QueryDocument>(repository);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?$filter=EventId%20eq%2014");

        var item = AssertOkPayload<QueryDocument>(await functions.Read(context.Request, 7));

        Assert.NotNull(item);
        Assert.Equal(7, item.Id);
        Assert.Equal(14, item.EventId);
        Assert.Null(repository.LastReadManyOptions);
    }

    [Fact]
    public async Task CrudFunctions_Delete_UsesRouteId()
    {
        var repository = new RecordingRepository<QueryDocument>();
        repository.Items.Add(new QueryDocument { Id = 7, EventId = 14 });
        repository.Items.Add(new QueryDocument { Id = 7, EventId = 99 });
        var functions = new TestCrudFunctions<QueryDocument>(repository);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?$filter=EventId%20eq%2014");

        await functions.Delete(context.Request, 7);

        var item = Assert.Single(repository.Deleted);
        Assert.Equal(7, item.Id);
        Assert.Equal(14, item.EventId);
        Assert.Null(repository.LastReadManyOptions);
    }

    [Fact]
    public async Task CrudFunctions_ReadMany_RejectsUnsupportedODataOptions()
    {
        var repository = new RecordingRepository<QueryDocument>();
        var functions = new TestCrudFunctions<QueryDocument>(repository);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?$orderby=Id");

        var result = await functions.ReadMany(context.Request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CrudFunctions_ReadMany_AcceptsStringEnumFilters()
    {
        var repository = new RecordingRepository<QueryEnumDocument>();
        repository.Items.Add(new QueryEnumDocument { Id = 1, Role = QueryRole.President });
        repository.Items.Add(new QueryEnumDocument { Id = 2, Role = QueryRole.Steward });
        var functions = new TestCrudFunctions<QueryEnumDocument>(repository);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?$filter=Role%20eq%20%27President%27");

        var items = AssertOkPayload<IEnumerable<QueryEnumDocument>>(await functions.ReadMany(context.Request));

        var item = Assert.Single(items);
        Assert.Equal(QueryRole.President, item.Role);
    }

    [Fact]
    public async Task CrudFunctions_Create_DoesNotApplyQueryValues()
    {
        var repository = new RecordingRepository<QueryDocument>();
        var functions = new TestCrudFunctions<QueryDocument>(repository);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?$filter=EventId%20eq%2014");
        context.Request.Body = CreateBody(new QueryDocument { Id = 1, EventId = 99 });

        await functions.Create(context.Request);

        Assert.Equal(99, repository.Created?.EventId);
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

    static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{nameof(NHttpSettings)}:{nameof(NHttpSettings.Host)}"] = "https://nexus.test",
                    [$"{nameof(NHttpSettings)}:{nameof(NHttpSettings.EndpointPrefix)}"] = "api",
                }
            )
            .Build();
    }

    static void AssertRegistration<TService, TImplementation>(IServiceCollection services)
    {
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(TService)
                && descriptor.ImplementationType == typeof(TImplementation)
        );
    }

    static void AssertNoRegistration<TService, TImplementation>(IServiceCollection services)
    {
        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(TService)
                && descriptor.ImplementationType == typeof(TImplementation)
        );
    }

    static OkObjectResult AssertOk(IActionResult result)
    {
        Assert.True(
            result is OkObjectResult,
            $"Expected {nameof(OkObjectResult)}, got {result.GetType().Name}: {(result as ObjectResult)?.Value}"
        );
        return (OkObjectResult)result;
    }

    static T AssertOkPayload<T>(IActionResult result)
    {
        var ok = AssertOk(result);
        var payload = Assert.IsType<Result<T>>(ok.Value);
        Assert.True(payload.IsSuccess, string.Join(Environment.NewLine, payload.Errors));
        return payload.Data!;
    }

    static void AssertRequest(RecordingHttpMessageHandler handler, string expectedUrl)
    {
        AssertRequest(handler, HttpMethod.Get, expectedUrl);
    }

    static void AssertRequest(
        RecordingHttpMessageHandler handler,
        HttpMethod expectedMethod,
        string expectedUrl
    )
    {
        var request = Assert.Single(handler.Requests);
        Assert.Equal(expectedMethod, request.Method);
        Assert.Equal(expectedUrl, request.RequestUri?.OriginalString);
    }

    static Stream CreateBody(object payload)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(payload.ToJson()));
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
        public List<string?> Bodies { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(request);
            Bodies.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync());
            return
                ResponseFactory?.Invoke(request)
                    ?? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(Result.Success().ToJson()),
                    };
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
        public SocketConnectionStatus Status =>
            IsConnected ? SocketConnectionStatus.Connected : SocketConnectionStatus.Disconnected;
        public EnduranceEvent? Event { get; set; }
    }

    sealed class TestCrudFunctions<T> : CrudFunctions<T>
        where T : class
    {
        public TestCrudFunctions(IMongoRepository<T> repository)
            : base(new TestFunctionLogger<CrudFunctions<T>>(), repository, new TestTelemetryService()) { }

        public Task<IActionResult> Create(HttpRequest request)
        {
            return CreateCore(request);
        }

        public Task<IActionResult> Read(HttpRequest request, int id)
        {
            return ReadCore(id);
        }

        public Task<IActionResult> ReadMany(HttpRequest request)
        {
            return ReadManyCore(request);
        }

        public Task<IActionResult> Delete(HttpRequest request, int id)
        {
            return DeleteCore(id);
        }
    }

    sealed class QueryDocument
    {
        public int Id { get; set; }
        public int EventId { get; set; }
    }

    sealed class QueryEnumDocument
    {
        public int Id { get; set; }
        public QueryRole Role { get; set; }
    }

    sealed class RecordingRepository<T> : IMongoRepository<T>
        where T : class
    {
        public List<T> Items { get; } = [];
        public List<T> Deleted { get; } = [];
        public T? Created { get; private set; }
        public Expression<Func<T, bool>>? LastReadManyFilter { get; private set; }
        public ODataQueryOptions<T>? LastReadManyOptions { get; private set; }

        public Task Create(T item)
        {
            Created = item;
            return Task.CompletedTask;
        }

        public Task<T?> Read(int id)
        {
            return Task.FromResult(Items.FirstOrDefault(x => GetId(x) == id));
        }

        public Task<T?> Read(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult<T?>(null);
        }

        public Task<IEnumerable<T>> ReadMany()
        {
            return Task.FromResult<IEnumerable<T>>([]);
        }

        public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
        {
            LastReadManyFilter = filter;
            return Task.FromResult<IEnumerable<T>>([]);
        }

        public Task<IEnumerable<T>> ReadMany(ODataQueryOptions<T> options)
        {
            LastReadManyOptions = options;
            return Task.FromResult(options.ApplyTo(Items.AsQueryable()).Cast<T>().AsEnumerable());
        }

        public Task Update(T item)
        {
            return Task.CompletedTask;
        }

        public Task Delete(int id)
        {
            var item = Items.FirstOrDefault(x => GetId(x) == id);
            if (item != null)
            {
                Deleted.Add(item);
            }
            return Task.CompletedTask;
        }

        public Task Delete(T item)
        {
            Deleted.Add(item);
            return Task.CompletedTask;
        }

        public Task DeleteMany(Expression<Func<T, bool>> filter)
        {
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<T> items)
        {
            return Task.CompletedTask;
        }

        static int? GetId(T item)
        {
            return item.GetType().GetProperty("Id")?.GetValue(item) as int?;
        }
    }

    sealed class TestTelemetryService : ITelemetryService
    {
        public System.Diagnostics.Activity? StartActivity(string className, string methodName)
        {
            return null;
        }
    }

    sealed class TestFunctionLogger<T> : IFunctionLogger<T>
    {
        public void LogDebug(HttpRequest request, string method = "") { }

        public void LogDebug(string template, HttpRequest request, object[] args, string method = "") { }

        public void LogInformation(HttpRequest request, string method = "") { }

        public void LogInformation(string template, HttpRequest request, object[] args, string method = "") { }

        public void LogError(HttpRequest request, string method = "") { }

        public void LogError(string template, HttpRequest request, object[] args, string method = "") { }

        public void LogError(HttpRequest request, Exception exception, string method = "") { }
    }

    sealed class QueryEntity : IEntity, IEventScoped
    {
        public QueryEntity(int id, int eventId)
        {
            Id = id;
            EventId = eventId;
        }

        public int Id { get; }
        public int EventId { get; }
    }

    sealed class QueryEventModel : IEventScoped, IKrudModel<QueryEntity>
    {
        public int Id { get; set; }
        public string TenantId { get; set; } = "nts";
        public int EventId { get; set; }

        public void MapFrom(QueryEntity entity)
        {
            Id = entity.Id;
            EventId = entity.EventId;
        }

        public QueryEntity MapToEntity()
        {
            return new QueryEntity(Id, EventId);
        }
    }

    sealed class QueryEventRepository : EventScopedApiRepository<QueryEntity, QueryEventModel>
    {
        public QueryEventRepository(NHttpClient client, INtsSocketContext socketContext)
            : base("query-documents", client, new EventScopeFactory<QueryEntity>(socketContext)) { }
    }

    enum QueryRole
    {
        President,
        Steward,
    }
}
