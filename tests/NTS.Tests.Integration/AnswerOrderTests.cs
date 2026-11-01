using System.Net;
using System.Net.Http.Headers;
using System.Text;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The order in which a request is answered (ADR-0012, #671): signed in (401), then the transport (415, 413, 400), then the
/// place it names, found once (404), then the Level against the row and then the stage of the place (403, 409), then the
/// content (400, 422), then the work. The first thing wrong is the answer, so a person who may not do an action never learns
/// what is wrong with its content, and a request for an Event that is not there is not told anything about who may. The
/// expectations are written from that order and not from what a route does, and they are tried on a route of each way a
/// place is named in a request: in the body.
/// </summary>
public sealed class AnswerOrderTests : IClassFixture<MongoFixture>
{
    public static TheoryData<string> Routes()
    {
        return new TheoryData<string> { "event-grants", "console-operators" };
    }

    readonly MongoFixture _mongo;

    public AnswerOrderTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task The_transport_is_answered_before_the_place_is_looked_for_and_before_anybody_is_asked_what_they_hold(
        string route
    )
    {
        await using var scene = await SceneAsync();
        var elsewhere = Guid.NewGuid();

        var notJsonApi = await SendRawAsync(scene.Refused(route), route, "text/plain", "{}");
        var notJson = await SendRawAsync(scene.Refused(route), route, "application/vnd.api+json", "not json");
        var wrongType = await SendRawAsync(
            scene.Refused(route),
            route,
            "application/vnd.api+json",
            $$"""{ "data": { "type": "athletes", "attributes": { "eventId": "{{elsewhere}}" } } }"""
        );

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJsonApi.StatusCode);
        Assert.Equal("unsupported-media-type", await ErrorCodeAsync(notJsonApi));
        Assert.Equal(HttpStatusCode.BadRequest, notJson.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(notJson));
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(wrongType));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_place_that_is_named_wrongly_is_400_and_one_that_is_not_there_is_404_to_everybody_who_is_signed_in(
        string route
    )
    {
        await using var scene = await SceneAsync();

        var notAnId = await PostAsync(scene.Refused(route), route, new { eventId = "not-a-guid", email = "a@x.test" });
        var missingForTheRefused = await PostAsync(
            scene.Refused(route),
            route,
            new { eventId = Guid.NewGuid(), email = "a@x.test" }
        );
        var missingForRoot = await PostAsync(scene.Root, route, new { eventId = Guid.NewGuid(), email = "a@x.test" });

        Assert.Equal(HttpStatusCode.BadRequest, notAnId.StatusCode);
        Assert.Equal("invalid-event", await ErrorCodeAsync(notAnId));
        Assert.Equal(HttpStatusCode.NotFound, missingForTheRefused.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingForRoot.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task What_the_caller_holds_is_answered_before_the_stage_and_before_the_content(string route)
    {
        await using var scene = await SceneAsync();
        var code = Needs(route);

        var onLiveWithBadContent = await PostAsync(
            scene.Refused(route),
            route,
            new { eventId = scene.Live, email = "not an email" }
        );
        var onHistoricWithGoodContent = await PostAsync(
            scene.Refused(route),
            route,
            new { eventId = scene.Historic, email = "a@x.test" }
        );

        Assert.Equal(HttpStatusCode.Forbidden, onLiveWithBadContent.StatusCode);
        Assert.Equal(code, await ErrorCodeAsync(onLiveWithBadContent));
        Assert.Equal(HttpStatusCode.Forbidden, onHistoricWithGoodContent.StatusCode);
        Assert.Equal(code, await ErrorCodeAsync(onHistoricWithGoodContent));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task The_stage_is_answered_before_the_content(string route)
    {
        await using var scene = await SceneAsync();

        var response = await PostAsync(scene.Root, route, new { eventId = scene.Historic, email = "not an email" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(response));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task The_content_is_answered_last_and_then_the_work_is_done(string route)
    {
        await using var scene = await SceneAsync();

        var bad = await PostAsync(scene.Root, route, new { eventId = scene.Live, email = "not an email" });
        var good = await PostAsync(
            scene.Root,
            route,
            new { eventId = scene.Live, email = UserSeed.NewEmail("invited") }
        );

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("invalid-email", await ErrorCodeAsync(bad));
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
    }

    /// <summary>The code of the refusal of the lowest caller that a route refuses: what the route needs, as the matrix of ADR-0012 says.</summary>
    static string Needs(string route)
    {
        return route == "console-operators" ? "not-root" : "not-console-operator";
    }

    Task<HttpResponseMessage> PostAsync(Person person, string route, object attributes)
    {
        var attributesWithKind = route == "event-grants" ? Merge(attributes, new { kind = "operator" }) : attributes;
        return person.Page.WriteAsync(HttpMethod.Post, $"/api/{route}", route, attributesWithKind);
    }

    static Dictionary<string, object?> Merge(object first, object second)
    {
        var merged = new Dictionary<string, object?>();
        foreach (var source in new[] { first, second })
        {
            foreach (var property in source.GetType().GetProperties())
            {
                merged[property.Name] = property.GetValue(source);
            }
        }

        return merged;
    }

    static Task<HttpResponseMessage> SendRawAsync(Person person, string route, string contentType, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/{route}")
        {
            Content = new StringContent(body, Encoding.UTF8),
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return person.Page.SendAsync(request);
    }

    async Task<Scene> SceneAsync()
    {
        var api = new ApiFactory(_mongo.ConnectionString);
        var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            consoleOperator.Id,
            DateTimeOffset.UtcNow
        );
        var historic = await EventSeed.HistoricAsync(
            _mongo.ConnectionString,
            tenant,
            consoleOperator.Id,
            DateTimeOffset.UtcNow
        );
        return new Scene(api, client, root, consoleOperator, member, live, historic);
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    sealed class Scene : IAsyncDisposable
    {
        readonly ApiFactory _api;
        readonly HttpClient _client;

        public Scene(
            ApiFactory api,
            HttpClient client,
            Person root,
            Person consoleOperator,
            Person member,
            Guid live,
            Guid historic
        )
        {
            _api = api;
            _client = client;
            Root = root;
            ConsoleOperator = consoleOperator;
            Member = member;
            Live = live;
            Historic = historic;
        }

        public Person Root { get; }
        public Person ConsoleOperator { get; }
        public Person Member { get; }
        public Guid Live { get; }
        public Guid Historic { get; }

        /// <summary>The lowest caller the route refuses: the account that holds nothing for the grants of an Event, and a Console Operator for the Console Operators.</summary>
        public Person Refused(string route)
        {
            return route == "console-operators" ? ConsoleOperator : Member;
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _api.DisposeAsync();
        }
    }
}
