using System.Net;
using System.Net.Http.Headers;
using System.Text;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The order in which a request is answered (ADR-0012, #671), tried on the routes whose place is not in the body: an Event
/// named by the route, an Event named by the id of the document, and the Tenant the caller acts in. The first thing wrong is
/// the answer: the transport, then the place (400 when it is named wrongly, 404 when it is not there), then what the caller
/// holds, then the stage, then the content. The expectations are written from that order and not from what a route does.
/// </summary>
public sealed class PlaceOrderTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public PlaceOrderTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_change_of_a_Setup_is_answered_by_the_transport_then_the_place_then_the_Level_then_the_stage_then_the_content()
    {
        await using var scene = await SceneAsync();
        var elsewhere = Guid.NewGuid();

        var notJsonApi = await SendRawAsync(
            scene.Member,
            HttpMethod.Patch,
            $"/api/configure-events/{elsewhere}",
            "text/plain",
            "{}"
        );
        var unknown = await ChangeSetupAsync(scene.Member, elsewhere.ToString(), new { name = "x" });
        var notAnId = await ChangeSetupAsync(scene.Member, "not-a-guid", new { name = "x" });
        var tooLittle = await ChangeSetupAsync(
            scene.Member,
            scene.Unstarted.ToString(),
            new { name = "x" },
            id: Guid.NewGuid().ToString()
        );
        var tooLate = await ChangeSetupAsync(
            scene.ConsoleOperator,
            scene.Live.ToString(),
            new { name = "x" },
            id: Guid.NewGuid().ToString()
        );
        var tooLittleAndTooLate = await ChangeSetupAsync(scene.Member, scene.Live.ToString(), new { name = "x" });
        var mismatched = await ChangeSetupAsync(
            scene.ConsoleOperator,
            scene.Unstarted.ToString(),
            new { name = "x" },
            id: Guid.NewGuid().ToString()
        );

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJsonApi.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notAnId.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // the Level comes before the document that does not agree with the route
        Assert.Equal("not-console-operator", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode); // the stage comes before it too
        Assert.Equal("event-started", await ErrorCodeAsync(tooLate));
        Assert.Equal("not-console-operator", await ErrorCodeAsync(tooLittleAndTooLate)); // and the Level before the stage
        Assert.Equal(HttpStatusCode.Conflict, mismatched.StatusCode);
        Assert.Equal("id-mismatch", await ErrorCodeAsync(mismatched));
    }

    [Fact]
    public async Task An_Event_is_started_from_the_Setup_the_document_names_and_the_name_is_answered_before_anything_about_who_asks()
    {
        await using var scene = await SceneAsync();

        var withoutAnId = await StartAsync(scene.Member, null, new { });
        var notAnId = await StartAsync(scene.Member, "not-a-guid", new { });
        var unknown = await StartAsync(scene.Member, Guid.NewGuid().ToString(), new { });
        var tooLittle = await StartAsync(scene.Member, scene.Unstarted.ToString(), new { nothing = "else" });
        var tooLate = await StartAsync(scene.Root, scene.Historic.ToString(), new { nothing = "else" });
        var content = await StartAsync(scene.ConsoleOperator, scene.Unstarted.ToString(), new { nothing = "else" });

        Assert.Equal(HttpStatusCode.BadRequest, withoutAnId.StatusCode);
        Assert.Equal("id-required", await ErrorCodeAsync(withoutAnId));
        Assert.Equal(HttpStatusCode.BadRequest, notAnId.StatusCode);
        Assert.Equal("invalid-id", await ErrorCodeAsync(notAnId));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // who asks before what is asked
        Assert.Equal("not-console-operator", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(tooLate));
        Assert.Equal(HttpStatusCode.BadRequest, content.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(content));
    }

    [Fact]
    public async Task An_Event_is_made_in_the_current_Tenant_of_the_account_which_is_looked_for_before_what_it_holds_and_the_content_last()
    {
        await using var scene = await SceneAsync();
        var homeless = await SignedInAsync(scene.Api, scene.Client, _mongo.ConnectionString, home: null);

        var noTenant = await MakeAsync(homeless, new { name = "x", location = "y" });
        var tooLittle = await MakeAsync(scene.Member, new { name = "", location = "" });
        var content = await MakeAsync(scene.Root, new { name = "", location = "Sofia" });
        var made = await MakeAsync(scene.Root, new { name = "x", location = "Sofia" });

        Assert.Equal(HttpStatusCode.Conflict, noTenant.StatusCode);
        Assert.Equal("no-current-tenant", await ErrorCodeAsync(noTenant));
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // a person who may not make one is not told what is wrong with it
        Assert.Equal("not-root", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.BadRequest, content.StatusCode);
        Assert.Equal("invalid-name", await ErrorCodeAsync(content));
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
    }

    static Task<HttpResponseMessage> ChangeSetupAsync(Person person, string route, object attributes, string? id = null)
    {
        return person.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/configure-events/{route}",
            "configure-events",
            attributes,
            id: id
        );
    }

    static Task<HttpResponseMessage> StartAsync(Person person, string? id, object attributes)
    {
        return person.Page.WriteAsync(HttpMethod.Post, "/api/events", "events", attributes, id: id);
    }

    static Task<HttpResponseMessage> MakeAsync(Person person, object attributes)
    {
        return person.Page.WriteAsync(HttpMethod.Post, "/api/configure-events", "configure-events", attributes);
    }

    static Task<HttpResponseMessage> SendRawAsync(
        Person person,
        HttpMethod method,
        string path,
        string contentType,
        string body
    )
    {
        var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8) };
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
        var now = DateTimeOffset.UtcNow;
        return new Scene(
            api,
            client,
            root,
            consoleOperator,
            member,
            await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Not yet"),
            await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, now),
            await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, now)
        );
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    sealed class Scene : IAsyncDisposable
    {
        public Scene(
            ApiFactory api,
            HttpClient client,
            Person root,
            Person consoleOperator,
            Person member,
            Guid unstarted,
            Guid live,
            Guid historic
        )
        {
            Api = api;
            Client = client;
            Root = root;
            ConsoleOperator = consoleOperator;
            Member = member;
            Unstarted = unstarted;
            Live = live;
            Historic = historic;
        }

        public ApiFactory Api { get; }
        public HttpClient Client { get; }
        public Person Root { get; }
        public Person ConsoleOperator { get; }
        public Person Member { get; }
        public Guid Unstarted { get; }
        public Guid Live { get; }
        public Guid Historic { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Api.DisposeAsync();
        }
    }
}
