using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Setup.Models;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Setup of an Event, the <c>configure-events</c> resource (#603, ADR-0012, ADR-0006, #671): what Root and the Console
/// Operators configure an Event with until it starts. Root makes the Event (see <c>EventAccessTests</c>) and Root or a
/// Console Operator of it configures it, and once it has started the Setup is not changed again, because the Console works
/// on the copies the Event made of it. The Setup is read by the people who run the Event, and by nobody else.
/// </summary>
public sealed class ConfigureEventRoutesTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = DateTimeOffset.UtcNow;

    readonly MongoFixture _mongo;

    public ConfigureEventRoutesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Tenant_Root_makes_an_Event_with_the_id_the_document_names_and_the_same_id_again_is_that_Event()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = Guid.NewGuid();

        var created = await CreateAsync(root, "First Name", id);
        var again = await CreateAsync(root, "Second Name", id);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/configure-events/{id}", created.Headers.Location?.ToString());
        Assert.Equal(
            id.ToString(),
            (await ApiSessions.ReadJsonAsync(created)).GetProperty("data").GetProperty("id").GetString()
        );
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(again)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("First Name", attributes.GetProperty("name").GetString());
        Assert.False(attributes.TryGetProperty("mainOperatorId", out _)); // nobody is assigned to run it (#671)
        var stored = (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal("First Name", stored["Name"].AsString);
        Assert.Equal(tenant, stored["TenantId"].AsString);
    }

    [Fact]
    public async Task A_Tenant_Root_makes_an_Event_with_everything_its_Setup_holds_in_one_request()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = Guid.NewGuid();
        var sent = ConfigureEventModel.From(SetupFactory.Full(id, "Full Setup"));

        var created = await CreateAsync(root, id, AttributesOf(sent));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(created)).GetProperty("data").GetProperty("attributes");
        var received = JsonSerializer.Deserialize<ConfigureEventModel>(
            attributes.GetRawText(),
            JsonApiResults.Options
        )!;
        received.Id = id;
        received.TenantId = sent.TenantId;
        received.MainOperatorId = sent.MainOperatorId;
        Assert.Equal(
            JsonSerializer.Serialize(sent, JsonApiResults.Options),
            JsonSerializer.Serialize(received, JsonApiResults.Options)
        );
        var stored = (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal(root.Id, stored["MainOperatorId"].AsGuid);
        Assert.Single(stored["Competitions"].AsBsonArray);
        Assert.Equal(2, stored["Loops"].AsBsonArray.Count);
    }

    [Fact]
    public async Task A_Setup_the_domain_does_not_accept_is_422_when_an_Event_is_made_with_it_and_none_is_made()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = Guid.NewGuid();
        var attributes = AttributesOf(ConfigureEventModel.From(SetupFactory.Full(id, "Twin loops")));
        var loops = JsonNode.Parse(attributes["loops"].GetRawText())!.AsArray();
        var twin = loops[0]!.DeepClone();
        twin["id"] = Guid.NewGuid().ToString();
        loops.Add(twin); // a second loop of the distance of the first
        attributes["loops"] = JsonSerializer.SerializeToElement(loops);

        var response = await CreateAsync(root, id, attributes);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid-attribute", await ErrorCodeAsync(response));
        Assert.Null(await EventSeed.SetupOfAsync(_mongo.ConnectionString, id));
    }

    [Fact]
    public async Task The_id_of_an_Event_of_another_Tenant_is_not_given_away_by_making_one_with_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var theirs = await EventSeed.SetupAsync(_mongo.ConnectionString, other, null, "Theirs");

        var response = await CreateAsync(root, "Mine", theirs);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("id-taken", await ErrorCodeAsync(response));
        Assert.Equal("Theirs", (await EventSeed.SetupOfAsync(_mongo.ConnectionString, theirs))!["Name"].AsString);
    }

    [Fact]
    public async Task The_Setup_is_read_by_the_Console_Operator_a_Tenant_Root_of_the_Tenant_and_the_Developer_and_by_nobody_else()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, other, isDeveloper: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var rootElsewhere = await SignedInAsync(api, client, _mongo.ConnectionString, other, TenantRootOf(other));
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Unstarted");
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        foreach (var eventId in new[] { unstarted, historic })
        {
            await EventSeed.GrantAsync(
                _mongo.ConnectionString,
                tenant,
                eventId,
                "Official",
                "Steward",
                official.Email,
                official.Id
            );
        }

        foreach (var eventId in new[] { unstarted, historic })
        {
            foreach (var reader in new[] { consoleOperator, root, developer })
            {
                var response = await reader.Page.GetAsync($"/api/configure-events/{eventId}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            foreach (var reader in new[] { member, official, rootElsewhere })
            {
                var response = await reader.Page.GetAsync($"/api/configure-events/{eventId}");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                Assert.Equal("not-console-operator", await ErrorCodeAsync(response));
            }
        }

        var missing = await root.Page.GetAsync($"/api/configure-events/{Guid.NewGuid()}");
        var malformed = await root.Page.GetAsync("/api/configure-events/not-a-guid");
        var anonymous = await client.GetAsync($"/api/configure-events/{unstarted}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task The_Setup_shows_the_Tenant_of_the_Event_and_what_it_was_configured_with_and_names_nobody_to_run_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);

        var response = await consoleOperator.Page.GetAsync($"/api/configure-events/{live}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
        Assert.Equal(tenant, attributes.GetProperty("tenantId").GetString());
        Assert.False(attributes.TryGetProperty("mainOperatorId", out _)); // who runs it is told by console-operators (#671)
        Assert.Equal("Sofia", attributes.GetProperty("location").GetString());
        Assert.Equal("BG", attributes.GetProperty("country").GetProperty("isoCode").GetString());
    }

    [Fact]
    public async Task The_Setups_listed_are_those_of_the_Tenant_to_a_Tenant_Root_and_the_ones_it_runs_to_a_Console_Operator()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Mine");
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, root.Id, "The root's");
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null, "Nobody's");
        await EventSeed.SetupAsync(_mongo.ConnectionString, other, consoleOperator.Id, "Elsewhere");

        Assert.Equal(["Mine", "Nobody's", "The root's"], Names(await ListAsync(root)).Order());
        Assert.Equal(["Mine"], Names(await ListAsync(consoleOperator)));
        Assert.Empty(Names(await ListAsync(member)));
        Assert.Equal(["The root's"], Names(await ListAsync(root, "?filter=name eq 'The root''s'")));
    }

    [Fact]
    public async Task Assigning_and_removing_a_Console_Operator_moves_the_Setup_of_a_Live_Event_into_and_out_of_its_list()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var first = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var second = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, first.Id, DateTimeOffset.UtcNow);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, first.Id, "Not started");

        var assigned = await root.Page.WriteAsync(
            HttpMethod.Post,
            "/api/console-operators",
            "console-operators",
            new { eventId = live, accountId = second.Id }
        );

        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        Assert.Equal([live], Ids(await ListAsync(second)));
        Assert.Equal(new[] { live, unstarted }.Order(), Ids(await ListAsync(first)).Order());

        var held = await EventSeed
            .Grants(_mongo.ConnectionString)
            .Find(
                new BsonDocument { { "EventId", EventSeed.Binary(live) }, { "AccountId", EventSeed.Binary(first.Id) } }
            )
            .SingleAsync();
        var removed = await root.Page.DeleteAsync($"/api/console-operators/{held["_id"].AsGuid}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal([unstarted], Ids(await ListAsync(first)));
        Assert.Equal(HttpStatusCode.Forbidden, (await first.Page.GetAsync($"/api/configure-events/{live}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.Page.GetAsync($"/api/configure-events/{live}")).StatusCode);
    }

    [Fact]
    public async Task A_Setup_with_everything_in_it_is_sent_by_a_Console_Operator_and_read_back_as_it_was_sent()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        var sent = ConfigureEventModel.From(SetupFactory.Full(id, "Full Setup"));

        var response = await ChangeAsync(consoleOperator, id, AttributesOf(sent));
        var read = await consoleOperator.Page.GetAsync($"/api/configure-events/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(read)).GetProperty("data").GetProperty("attributes");
        var received = JsonSerializer.Deserialize<ConfigureEventModel>(
            attributes.GetRawText(),
            JsonApiResults.Options
        )!;
        received.Id = id;
        received.TenantId = sent.TenantId;
        Assert.Equal(
            JsonSerializer.Serialize(sent, JsonApiResults.Options),
            JsonSerializer.Serialize(received, JsonApiResults.Options)
        );
        var stored = (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal("Full Setup", stored["Name"].AsString);
        Assert.Single(stored["Competitions"].AsBsonArray);
    }

    [Fact]
    public async Task A_change_names_the_members_it_changes_and_leaves_the_rest_of_the_Setup_as_it_was()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        await ChangeAsync(consoleOperator, id, AttributesOf(ConfigureEventModel.From(SetupFactory.Full(id))));

        var renamed = await ChangeAsync(consoleOperator, id, new { name = "Renamed", feiShowId = (string?)null });

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var stored = (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal("Renamed", stored["Name"].AsString);
        Assert.False(stored.Contains("FeiShowId"));
        Assert.Single(stored["Competitions"].AsBsonArray);
        Assert.Equal(2, stored["Loops"].AsBsonArray.Count);
        Assert.Equal("Sofia", stored["Location"].AsString);
    }

    [Fact]
    public async Task Only_a_Console_Operator_and_Root_change_a_Setup_and_an_account_that_holds_nothing_does_not()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Original");

        var refused = await ChangeAsync(member, id, new { name = "Taken" });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("not-console-operator", await ErrorCodeAsync(refused));

        var anonymous = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/configure-events/{id}"));
        Assert.Equal("Original", (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!["Name"].AsString);
        Assert.Equal(HttpStatusCode.OK, (await ChangeAsync(root, id, new { name = "By Root" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeAsync(developer, id, new { name = "By the Developer" })).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeAsync(consoleOperator, id, new { name = "By the Console Operator" })).StatusCode
        );
        Assert.True(anonymous.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_change_is_of_the_Event_the_route_names_and_a_document_of_another_id_is_a_conflict()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Kept Name");

        var response = await consoleOperator.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/configure-events/{id}",
            "configure-events",
            new { name = "Renamed" },
            id: Guid.NewGuid().ToString()
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("id-mismatch", await ErrorCodeAsync(response));
        Assert.Equal("Kept Name", (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!["Name"].AsString);
    }

    [Fact]
    public async Task A_Setup_that_the_Event_has_started_from_is_not_changed_and_the_answer_says_why()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);

        var duringTheEvent = await ChangeAsync(consoleOperator, live, new { name = "Too late" });
        var afterTheEvent = await ChangeAsync(consoleOperator, historic, new { name = "Far too late" });

        Assert.Equal(HttpStatusCode.Conflict, duringTheEvent.StatusCode);
        Assert.Equal("event-started", await ErrorCodeAsync(duringTheEvent));
        Assert.Equal(HttpStatusCode.Conflict, afterTheEvent.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(afterTheEvent));
        Assert.StartsWith("Event ", (await EventSeed.SetupOfAsync(_mongo.ConnectionString, live))!["Name"].AsString);
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("mainOperatorId")]
    [InlineData("id")]
    [InlineData("bogus")]
    public async Task A_change_cannot_give_a_Setup_a_Tenant_an_owner_or_a_member_it_does_not_have(string member)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Original");

        var response = await ChangeAsync(
            consoleOperator,
            id,
            new Dictionary<string, object> { ["name"] = "Taken", [member] = Guid.NewGuid() }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(response));
        Assert.Equal("Original", (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!["Name"].AsString);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("location")]
    [InlineData("country")]
    public async Task A_Setup_the_domain_does_not_accept_is_422_and_nothing_is_written(string member)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Original");

        var response = await ChangeAsync(consoleOperator, id, new Dictionary<string, object?> { [member] = null });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid-attribute", await ErrorCodeAsync(response));
        var stored = (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal("Original", stored["Name"].AsString);
        Assert.Equal("Sofia", stored["Location"].AsString);
        Assert.True(stored.Contains("Country"));
    }

    [Fact]
    public async Task A_Tenant_Root_deletes_an_Event_that_has_not_started_and_the_grants_of_the_Event_go_with_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        var kept = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        await EventSeed.GrantAsync(_mongo.ConnectionString, tenant, id, "Operator", null, "one@example.test", null);
        await EventSeed.GrantAsync(_mongo.ConnectionString, tenant, kept, "Operator", null, "two@example.test", null);

        var refusedForTheConsoleOperator = await consoleOperator.Page.DeleteAsync($"/api/configure-events/{id}");
        var removed = await root.Page.DeleteAsync($"/api/configure-events/{id}");
        var again = await root.Page.DeleteAsync($"/api/configure-events/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, refusedForTheConsoleOperator.StatusCode);
        Assert.Equal("not-root", await ErrorCodeAsync(refusedForTheConsoleOperator));
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Null(await EventSeed.SetupOfAsync(_mongo.ConnectionString, id));
        Assert.NotNull(await EventSeed.SetupOfAsync(_mongo.ConnectionString, kept));
        Assert.Empty(
            await EventSeed
                .Grants(_mongo.ConnectionString)
                .Find(new BsonDocument("EventId", EventSeed.Binary(id)))
                .ToListAsync()
        );
        Assert.Equal(
            2, // the Console Operator and the Operator
            (
                await EventSeed
                    .Grants(_mongo.ConnectionString)
                    .Find(new BsonDocument("EventId", EventSeed.Binary(kept)))
                    .ToListAsync()
            ).Count
        );
    }

    [Fact]
    public async Task An_Event_that_has_started_is_not_deleted_by_anybody_and_the_Developer_deletes_one_that_has_not()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, root.Id, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, root.Id, NOW);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);

        var duringTheEvent = await root.Page.DeleteAsync($"/api/configure-events/{live}");
        var afterTheEvent = await developer.Page.DeleteAsync($"/api/configure-events/{historic}");
        var byTheDeveloper = await developer.Page.DeleteAsync($"/api/configure-events/{unstarted}");

        Assert.Equal(HttpStatusCode.Conflict, duringTheEvent.StatusCode);
        Assert.Equal("event-started", await ErrorCodeAsync(duringTheEvent));
        Assert.Equal(HttpStatusCode.Conflict, afterTheEvent.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(afterTheEvent));
        Assert.Equal(HttpStatusCode.NoContent, byTheDeveloper.StatusCode);
        Assert.NotNull(await EventSeed.SetupOfAsync(_mongo.ConnectionString, live));
        Assert.NotNull(await EventSeed.SetupOfAsync(_mongo.ConnectionString, historic));
    }

    static Task<HttpResponseMessage> CreateAsync(Person person, string name, Guid? id = null)
    {
        return CreateAsync(person, id, new { name, location = "Sofia" });
    }

    static Task<HttpResponseMessage> CreateAsync(Person person, Guid? id, object attributes)
    {
        return person.Page.WriteAsync(
            HttpMethod.Post,
            "/api/configure-events",
            "configure-events",
            attributes,
            id: id?.ToString()
        );
    }

    static Task<HttpResponseMessage> ChangeAsync(Person person, Guid id, object attributes)
    {
        return person.Page.WriteAsync(HttpMethod.Patch, $"/api/configure-events/{id}", "configure-events", attributes);
    }

    /// <summary>The members of a Setup as a document carries them: everything but what the server owns.</summary>
    static Dictionary<string, JsonElement> AttributesOf(ConfigureEventModel model)
    {
        return JsonSerializer
            .SerializeToElement(model, JsonApiResults.Options)
            .EnumerateObject()
            .Where(x => x.Name is not ("id" or "tenantId"))
            .ToDictionary(x => x.Name, x => x.Value);
    }

    static async Task<JsonElement> ListAsync(Person person, string query = "")
    {
        var response = await person.Page.GetAsync("/api/configure-events" + query);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{query} answered {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"
        );
        return await ApiSessions.ReadJsonAsync(response);
    }

    static IEnumerable<Guid> Ids(JsonElement list)
    {
        return list.GetProperty("data").EnumerateArray().Select(x => Guid.Parse(x.GetProperty("id").GetString()!));
    }

    static IEnumerable<string> Names(JsonElement list)
    {
        return list.GetProperty("data")
            .EnumerateArray()
            .Select(x => x.GetProperty("attributes").GetProperty("name").GetString()!);
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
