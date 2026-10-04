using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// Who creates an Event, who runs it and who may hand it on (ADR-0012, #643). A Tenant Root creates the Events of its
/// Tenant and is each one's Main Operator until it assigns another account while the Event has not started; once the
/// Event is Live only the Main Operator hands it over, and neither a Tenant Root nor the Developer takes it. The Events
/// are made Live or Historic by the end that is stored for them against the clock of the host, which the tests fix.
/// </summary>
public sealed class EventAccessTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = DateTimeOffset.UtcNow;

    readonly MongoFixture _mongo;

    public EventAccessTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Tenant_Root_creates_an_Event_in_its_Tenant_and_is_its_Main_Operator()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        const string tenantName = "Bulgaria";
        var tenant = await TenantAsync(_mongo.ConnectionString, tenantName, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        var created = await CreateAsync(root, "Spring Ride", "Sofia", "FEI123");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(created)).GetProperty("data");
        Assert.Equal("configure-events", resource.GetProperty("type").GetString());
        var id = Guid.Parse(resource.GetProperty("id").GetString()!);
        var attributes = resource.GetProperty("attributes");
        Assert.Equal("Spring Ride", attributes.GetProperty("name").GetString());
        Assert.Equal("Sofia", attributes.GetProperty("location").GetString());
        Assert.Equal("FEI123", attributes.GetProperty("feiShowId").GetString());
        Assert.Equal(tenant, attributes.GetProperty("tenantId").GetString());
        Assert.Equal(root.Id.ToString(), attributes.GetProperty("mainOperatorId").GetString());
        var stored = (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal(root.Id, stored["MainOperatorId"].AsGuid);
        Assert.Equal("Spring Ride", stored["Name"].AsString);
        Assert.Equal("Sofia", stored["Location"].AsString);
        Assert.Equal("FEI123", stored["FeiShowId"].AsString);
        Assert.Equal(tenantName, stored["Country"].AsBsonDocument["Name"].AsString);
        Assert.Empty(stored["Competitions"].AsBsonArray);
        Assert.Empty(stored["Officials"].AsBsonArray);
        Assert.Empty(stored["Operators"].AsBsonArray);
        Assert.True((await CapabilitiesAsync(root, id)).GetProperty("isMainOperator").GetBoolean());
    }

    [Fact]
    public async Task The_Event_it_makes_has_the_country_of_its_Tenant()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, "Turkey", withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        var created = await CreateAsync(root, "Autumn Ride", "Izmir");

        var id = Guid.Parse(
            (await ApiSessions.ReadJsonAsync(created)).GetProperty("data").GetProperty("id").GetString()!
        );
        var country = (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!["Country"].AsBsonDocument;
        Assert.Equal("Turkey", country["Name"].AsString);
        Assert.Equal(tenant["country-".Length..].ToUpperInvariant(), country["IsoCode"].AsString.ToUpperInvariant());
    }

    [Fact]
    public async Task Only_a_Tenant_Root_of_the_Tenant_the_account_acts_in_creates_an_Event_there()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant)); // makes it operational
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var rootElsewhere = await SignedInAsync(api, client, _mongo.ConnectionString, other, TenantRootOf(other));
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var before = await EventSeed
            .Setups(_mongo.ConnectionString)
            .CountDocumentsAsync(new BsonDocument("TenantId", tenant));

        var byAMember = await CreateAsync(member, "Nope", "Sofia");
        var byAMainOperator = await CreateAsync(mainOperator, "Nope", "Sofia");
        var byARootElsewhere = await CreateAsync(rootElsewhere, "Mine", "Izmir");

        foreach (var refused in new[] { byAMember, byAMainOperator })
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Equal("not-tenant-root", await ErrorCodeAsync(refused));
        }

        // A Tenant Root of another Tenant makes the Event in that Tenant, which is the one it acts in: never in this one.
        Assert.Equal(HttpStatusCode.Created, byARootElsewhere.StatusCode);
        Assert.Equal(
            other,
            (await ApiSessions.ReadJsonAsync(byARootElsewhere))
                .GetProperty("data")
                .GetProperty("attributes")
                .GetProperty("tenantId")
                .GetString()
        );
        Assert.Equal(
            before,
            await EventSeed.Setups(_mongo.ConnectionString).CountDocumentsAsync(new BsonDocument("TenantId", tenant))
        );
    }

    [Fact]
    public async Task An_Event_is_made_in_the_Tenant_the_account_has_selected_and_not_in_its_home_Tenant()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var home = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var elsewhere = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, home, TenantRootOf(elsewhere));

        var inTheHome = await CreateAsync(root, "Not here", "Sofia");
        await SelectAsync(root, elsewhere);
        var selected = await CreateAsync(root, "Here", "Sofia");

        Assert.Equal(HttpStatusCode.Forbidden, inTheHome.StatusCode);
        Assert.Equal(HttpStatusCode.Created, selected.StatusCode);
        Assert.Equal(
            elsewhere,
            (await ApiSessions.ReadJsonAsync(selected))
                .GetProperty("data")
                .GetProperty("attributes")
                .GetProperty("tenantId")
                .GetString()
        );
    }

    [Fact]
    public async Task A_Tenant_that_has_no_Tenant_Root_holds_no_Event_even_for_the_Developer()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);

        var response = await CreateAsync(developer, "Nobody's", "Sofia");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("tenant-not-operational", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task The_Developer_creates_an_Event_in_a_Tenant_that_is_operational_and_is_its_Main_Operator()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);

        var response = await CreateAsync(developer, "By the Developer", "Sofia");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
        Assert.Equal(developer.Id.ToString(), attributes.GetProperty("mainOperatorId").GetString());
    }

    [Fact]
    public async Task An_account_that_has_no_current_Tenant_creates_no_Event()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);

        var response = await CreateAsync(person, "Nowhere", "Sofia");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("no-current-tenant", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_Tenant_whose_country_is_not_there_holds_no_Event_and_none_is_made()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: false);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var before = await EventSeed
            .Setups(_mongo.ConnectionString)
            .CountDocumentsAsync(new BsonDocument("TenantId", tenant));

        var response = await CreateAsync(root, "No country", "Sofia");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("tenant-country-missing", await ErrorCodeAsync(response));
        Assert.Equal(
            before,
            await EventSeed.Setups(_mongo.ConnectionString).CountDocumentsAsync(new BsonDocument("TenantId", tenant))
        );
    }

    [Theory]
    [InlineData("", "Sofia", "invalid-name")]
    [InlineData("   ", "Sofia", "invalid-name")]
    [InlineData("Spring\nRide", "Sofia", "invalid-name")]
    [InlineData("Ride", "", "invalid-location")]
    [InlineData("Ride", "So\tfia", "invalid-location")]
    public async Task An_Event_needs_a_name_and_a_location_of_one_line_each(string name, string location, string code)
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        var response = await CreateAsync(root, name, location);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("mainOperatorId")]
    [InlineData("id")]
    [InlineData("competitions")]
    public async Task A_new_Event_takes_nothing_but_its_name_location_and_FEI_show_so_it_cannot_be_given_a_Tenant_or_an_owner(
        string member
    )
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var attributes = new Dictionary<string, object>
        {
            ["name"] = "Ride",
            ["location"] = "Sofia",
            [member] = member == "competitions" ? Array.Empty<object>() : other,
        };

        var response = await root.Page.WriteAsync(
            HttpMethod.Post,
            "/api/configure-events",
            "configure-events",
            attributes
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_Tenant_Root_assigns_another_account_as_the_Main_Operator_while_the_Event_has_not_started()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, root.Id);

        var assigned = await AssignAsync(root, id, colleague.Id);

        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        Assert.Equal(
            colleague.Id,
            (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!["MainOperatorId"].AsGuid
        );
        Assert.True((await CapabilitiesAsync(colleague, id)).GetProperty("isMainOperator").GetBoolean());
        Assert.False((await CapabilitiesAsync(root, id)).GetProperty("isMainOperator").GetBoolean());
    }

    [Fact]
    public async Task The_Tenant_Root_can_assign_again_and_take_the_Event_back_while_it_has_not_started()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, colleague.Id);

        var back = await AssignAsync(root, id, root.Id);

        Assert.Equal(HttpStatusCode.NoContent, back.StatusCode);
        Assert.True((await CapabilitiesAsync(root, id)).GetProperty("isMainOperator").GetBoolean());
    }

    [Fact]
    public async Task The_account_a_Tenant_Root_assigns_has_to_exist()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, root.Id);

        var response = await AssignAsync(root, id, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("account-not-found", await ErrorCodeAsync(response));
        Assert.Equal(root.Id, (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!["MainOperatorId"].AsGuid);
    }

    [Theory]
    [InlineData("assign-main-operator", "tenantId")]
    [InlineData("assign-main-operator", "mainOperatorId")]
    [InlineData("hand-over", "tenantId")]
    [InlineData("hand-over", "email")]
    public async Task A_change_of_Main_Operator_takes_the_account_and_nothing_else_and_changes_nothing_when_it_is_refused(
        string action,
        string member
    )
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var handOver = action == "hand-over";
        var id = handOver
            ? await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW)
            : await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var caller = handOver ? mainOperator : root;
        var path = $"/api/events/{id}/actions/{action}";

        var extra = await caller.Page.WriteAsync(
            HttpMethod.Post,
            path,
            "main-operators",
            new Dictionary<string, object> { ["accountId"] = colleague.Id, [member] = "anything" }
        );
        var nobody = await caller.Page.WriteAsync(
            HttpMethod.Post,
            path,
            "main-operators",
            new Dictionary<string, object>()
        );
        var notAnAccount = await caller.Page.WriteAsync(
            HttpMethod.Post,
            path,
            "main-operators",
            new { accountId = "not-a-guid" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(extra));
        Assert.Equal(HttpStatusCode.BadRequest, nobody.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(nobody));
        Assert.Equal(HttpStatusCode.BadRequest, notAnAccount.StatusCode);
        var stored = handOver
            ? await EventSeed.CoreOfAsync(_mongo.ConnectionString, id)
            : await EventSeed.SetupOfAsync(_mongo.ConnectionString, id);
        Assert.Equal(mainOperator.Id, stored!["MainOperatorId"].AsGuid);
    }

    [Fact]
    public async Task Only_a_Tenant_Root_of_the_Tenant_assigns_the_Main_Operator_and_not_the_Main_Operator_itself()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var rootElsewhere = await SignedInAsync(api, client, _mongo.ConnectionString, other, TenantRootOf(other));
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);

        foreach (var caller in new[] { mainOperator, member, rootElsewhere })
        {
            var response = await AssignAsync(caller, id, caller.Id);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("not-tenant-root", await ErrorCodeAsync(response));
        }

        Assert.Equal(
            mainOperator.Id,
            (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!["MainOperatorId"].AsGuid
        );
    }

    [Fact]
    public async Task The_Developer_assigns_the_Main_Operator_of_an_Event_that_has_not_started_in_any_Tenant()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, home: null, isDeveloper: true);
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);

        var response = await AssignAsync(developer, id, colleague.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            colleague.Id,
            (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!["MainOperatorId"].AsGuid
        );
    }

    [Fact]
    public async Task Nobody_assigns_the_Main_Operator_of_an_Event_once_it_is_Live_and_certainly_not_once_it_is_Historic()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);

        var rootOnLive = await AssignAsync(root, live, root.Id);
        var developerOnLive = await AssignAsync(developer, live, developer.Id);
        var rootOnHistoric = await AssignAsync(root, historic, root.Id);

        Assert.Equal(HttpStatusCode.Conflict, rootOnLive.StatusCode);
        Assert.Equal("event-started", await ErrorCodeAsync(rootOnLive));
        Assert.Equal("event-started", await ErrorCodeAsync(developerOnLive));
        Assert.Equal(HttpStatusCode.Conflict, rootOnHistoric.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(rootOnHistoric));
        Assert.Equal(
            mainOperator.Id,
            (await EventSeed.CoreOfAsync(_mongo.ConnectionString, live))!["MainOperatorId"].AsGuid
        );
    }

    [Fact]
    public async Task The_Main_Operator_hands_a_Live_Event_to_a_named_account_and_stops_being_its_Main_Operator()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);

        var handed = await HandOverAsync(mainOperator, live, colleague.Id);

        Assert.Equal(HttpStatusCode.NoContent, handed.StatusCode);
        Assert.Equal(
            colleague.Id,
            (await EventSeed.CoreOfAsync(_mongo.ConnectionString, live))!["MainOperatorId"].AsGuid
        );
        Assert.True((await CapabilitiesAsync(colleague, live)).GetProperty("isMainOperator").GetBoolean());
        var former = await CapabilitiesAsync(mainOperator, live);
        Assert.False(former.GetProperty("isMainOperator").GetBoolean());
        Assert.False(former.GetProperty("canHandOver").GetBoolean());
        var again = await HandOverAsync(mainOperator, live, mainOperator.Id); // what it no longer holds it cannot give
        Assert.Equal(HttpStatusCode.Forbidden, again.StatusCode);
    }

    [Fact]
    public async Task Only_the_current_Main_Operator_hands_a_Live_Event_over_and_neither_a_Tenant_Root_nor_the_Developer_can_take_it()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);

        foreach (var caller in new[] { root, developer, member })
        {
            var response = await HandOverAsync(caller, live, caller.Id);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("not-main-operator", await ErrorCodeAsync(response));
        }

        Assert.Equal(
            mainOperator.Id,
            (await EventSeed.CoreOfAsync(_mongo.ConnectionString, live))!["MainOperatorId"].AsGuid
        );
    }

    [Fact]
    public async Task An_Event_is_handed_over_only_while_it_is_Live()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);

        var beforeTheStart = await HandOverAsync(mainOperator, unstarted, colleague.Id);
        var afterTheEnd = await HandOverAsync(mainOperator, historic, colleague.Id);

        Assert.Equal(HttpStatusCode.Conflict, beforeTheStart.StatusCode);
        Assert.Equal("event-not-started", await ErrorCodeAsync(beforeTheStart));
        Assert.Equal(HttpStatusCode.Conflict, afterTheEnd.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(afterTheEnd));
    }

    [Fact]
    public async Task An_Event_that_is_not_there_is_not_found_by_anything_asked_of_it()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var missing = Guid.NewGuid();

        var assigned = await AssignAsync(root, missing, root.Id);
        var handed = await HandOverAsync(root, missing, root.Id);
        var capabilities = await root.Page.GetAsync($"/api/events/{missing}/capabilities");

        Assert.Equal(HttpStatusCode.NotFound, assigned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, handed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, capabilities.StatusCode);
    }

    [Fact]
    public async Task The_clock_decides_when_an_Event_is_Live_and_when_it_is_not()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var end = NOW.AddHours(6);
        await EventSeed.StartAsync(_mongo.ConnectionString, id, tenant, mainOperator.Id, end);

        time.SetUtcNow(end.AddSeconds(-1));
        var oneSecondBefore = await CapabilitiesAsync(mainOperator, id);
        time.SetUtcNow(end);
        var atTheEnd = await CapabilitiesAsync(mainOperator, id);

        Assert.Equal("live", oneSecondBefore.GetProperty("stage").GetString());
        Assert.True(oneSecondBefore.GetProperty("canHandOver").GetBoolean());
        Assert.Equal("historic", atTheEnd.GetProperty("stage").GetString());
        Assert.False(atTheEnd.GetProperty("canHandOver").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await HandOverAsync(mainOperator, id, colleague.Id)).StatusCode);
    }

    [Fact]
    public async Task What_the_capabilities_say_is_what_the_policy_allows_the_caller_at_each_stage()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, other, isDeveloper: true);
        var rootElsewhere = await SignedInAsync(api, client, _mongo.ConnectionString, other, TenantRootOf(other));
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);

        // What ADR-0012 allows each, by hand: (isMainOperator, canSnapshot, canHandOver, canAssignMainOperator, canCreateEvents)
        await AssertCapabilitiesAsync(mainOperator, unstarted, true, false, false, false, false);
        await AssertCapabilitiesAsync(mainOperator, live, true, true, true, false, false);
        await AssertCapabilitiesAsync(mainOperator, historic, true, false, false, false, false);
        await AssertCapabilitiesAsync(root, unstarted, false, false, false, true, true);
        await AssertCapabilitiesAsync(root, live, false, false, false, false, true);
        await AssertCapabilitiesAsync(root, historic, false, false, false, false, true);
        await AssertCapabilitiesAsync(member, unstarted, false, false, false, false, false);
        await AssertCapabilitiesAsync(member, live, false, false, false, false, false);
        await AssertCapabilitiesAsync(developer, unstarted, false, false, false, true, true);
        await AssertCapabilitiesAsync(developer, live, false, false, false, false, true);
        await AssertCapabilitiesAsync(rootElsewhere, unstarted, false, false, false, false, false);
        await AssertCapabilitiesAsync(rootElsewhere, live, false, false, false, false, false);
    }

    async Task AssertCapabilitiesAsync(
        Person person,
        Guid id,
        bool isMainOperator,
        bool canSnapshot,
        bool canHandOver,
        bool canAssignMainOperator,
        bool canCreateEvents
    )
    {
        var capabilities = await CapabilitiesAsync(person, id);
        Assert.Equal(isMainOperator, capabilities.GetProperty("isMainOperator").GetBoolean());
        Assert.Equal(canSnapshot, capabilities.GetProperty("canSnapshot").GetBoolean());
        Assert.Equal(canHandOver, capabilities.GetProperty("canHandOver").GetBoolean());
        Assert.Equal(canAssignMainOperator, capabilities.GetProperty("canAssignMainOperator").GetBoolean());
        Assert.Equal(canCreateEvents, capabilities.GetProperty("canCreateEvents").GetBoolean());
    }

    ApiFactory NewApi()
    {
        return NewApi(out _);
    }

    ApiFactory NewApi(out FakeTimeProvider time)
    {
        time = new FakeTimeProvider(NOW);
        return new ApiFactory(_mongo.ConnectionString, time: time);
    }

    static Task<HttpResponseMessage> CreateAsync(Person person, string name, string location, string? feiShowId = null)
    {
        return person.Page.WriteAsync(
            HttpMethod.Post,
            "/api/configure-events",
            "configure-events",
            new
            {
                name,
                location,
                feiShowId,
            }
        );
    }

    static Task<HttpResponseMessage> SelectAsync(Person person, string tenant)
    {
        return person.Page.WriteAsync(HttpMethod.Patch, "/api/me", "accounts", new { selectedTenantId = tenant });
    }

    static Task<HttpResponseMessage> AssignAsync(Person person, Guid eventId, Guid account)
    {
        return person.Page.WriteAsync(
            HttpMethod.Post,
            $"/api/events/{eventId}/actions/assign-main-operator",
            "main-operators",
            new { accountId = account }
        );
    }

    static Task<HttpResponseMessage> HandOverAsync(Person person, Guid eventId, Guid account)
    {
        return person.Page.WriteAsync(
            HttpMethod.Post,
            $"/api/events/{eventId}/actions/hand-over",
            "main-operators",
            new { accountId = account }
        );
    }

    static async Task<JsonElement> CapabilitiesAsync(Person person, Guid eventId)
    {
        var response = await person.Page.GetAsync($"/api/events/{eventId}/capabilities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal("capabilities", resource.GetProperty("type").GetString());
        Assert.Equal(eventId.ToString(), resource.GetProperty("id").GetString());
        return resource.GetProperty("attributes");
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
