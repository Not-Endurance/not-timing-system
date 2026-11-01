using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// Who creates an Event, who runs it and who assigns its Console Operators (ADR-0012, #643, #671). Root, which is a Tenant
/// Root of the Tenant or the Developer, creates the Events of the Tenant and runs every one of them from the moment it is
/// made, and assigns and removes the Console Operators of an Event, who run it beside Root, until the Event has ended; nobody
/// else does. Nobody is assigned when an Event is made. The Events are made Live or Historic by the end that is stored for
/// them against the clock of the host, which the tests fix.
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
    public async Task A_Tenant_Root_creates_an_Event_in_its_Tenant_and_runs_it_with_nobody_assigned()
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
        Assert.False(attributes.TryGetProperty("mainOperatorId", out _)); // the Main Operator is retired (#671)
        var stored = (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.False(stored.Contains("MainOperatorId"));
        Assert.Equal("Spring Ride", stored["Name"].AsString);
        Assert.Equal("Sofia", stored["Location"].AsString);
        Assert.Equal("FEI123", stored["FeiShowId"].AsString);
        Assert.Equal(tenantName, stored["Country"].AsBsonDocument["Name"].AsString);
        Assert.Empty(stored["Competitions"].AsBsonArray);
        Assert.Empty(stored["Officials"].AsBsonArray);
        Assert.Empty(stored["Operators"].AsBsonArray);
        Assert.Empty(await GrantsOfAsync(id));
        var capabilities = await CapabilitiesAsync(root, id);
        Assert.Equal("root", capabilities.GetProperty("level").GetString());
        Assert.True(capabilities.GetProperty("canConfigure").GetBoolean());
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
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        var before = await EventSeed
            .Setups(_mongo.ConnectionString)
            .CountDocumentsAsync(new BsonDocument("TenantId", tenant));

        var byAMember = await CreateAsync(member, "Nope", "Sofia");
        var byAConsoleOperator = await CreateAsync(consoleOperator, "Nope", "Sofia");
        var byARootElsewhere = await CreateAsync(rootElsewhere, "Mine", "Izmir");

        foreach (var refused in new[] { byAMember, byAConsoleOperator })
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Equal("not-root", await ErrorCodeAsync(refused));
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
    public async Task The_Developer_creates_an_Event_in_a_Tenant_that_is_operational_and_runs_it_as_Root()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);

        var response = await CreateAsync(developer, "By the Developer", "Sofia");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = Guid.Parse(
            (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetString()!
        );
        Assert.Equal("root", (await CapabilitiesAsync(developer, id)).GetProperty("level").GetString());
        Assert.Empty(await GrantsOfAsync(id));
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
    public async Task A_new_Event_cannot_be_given_a_Tenant_or_an_owner_by_what_is_sent(string member)
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
            [member] = other,
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
    public async Task Root_assigns_an_account_as_a_Console_Operator_of_an_Event_that_has_not_started_and_it_then_runs_it()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Vera Koleva");
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);

        var assigned = await AssignAsync(root, new { eventId = id, accountId = colleague.Id });

        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(assigned)).GetProperty("data");
        Assert.Equal("console-operators", resource.GetProperty("type").GetString());
        var attributes = resource.GetProperty("attributes");
        Assert.Equal(id.ToString(), attributes.GetProperty("eventId").GetString());
        Assert.False(attributes.GetProperty("pending").GetBoolean());
        Assert.Equal(colleague.Id.ToString(), attributes.GetProperty("accountId").GetString());
        Assert.Equal("Vera Koleva", attributes.GetProperty("displayName").GetString());
        Assert.DoesNotContain(colleague.Email, attributes.ToString()); // the address is told partly masked
        var stored = Assert.Single(await GrantsOfAsync(id));
        Assert.Equal("ConsoleOperator", stored["Kind"].AsString);
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal(colleague.Id, stored["AccountId"].AsGuid);
        Assert.Equal(colleague.Email, stored["Email"].AsString);
        Assert.Equal("consoleOperator", (await CapabilitiesAsync(colleague, id)).GetProperty("level").GetString());
        Assert.Equal("root", (await CapabilitiesAsync(root, id)).GetProperty("level").GetString()); // and Root still does
    }

    [Fact]
    public async Task An_account_found_by_name_is_assigned_by_its_id_and_the_same_one_twice_is_one_Console_Operator()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);

        var first = await AssignAsync(root, new { eventId = id, email = colleague.Email });
        var again = await AssignAsync(root, new { eventId = id, accountId = colleague.Id });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(await GrantIdAsync(first), await GrantIdAsync(again));
        Assert.Single(await GrantsOfAsync(id));
    }

    [Fact]
    public async Task An_email_that_has_no_account_is_a_pending_Console_Operator_that_names_nobody_and_runs_nothing()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);
        var email = UserSeed.NewEmail("invited");

        var assigned = await AssignAsync(root, new { eventId = id, email });

        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(assigned)).GetProperty("data").GetProperty("attributes");
        Assert.True(attributes.GetProperty("pending").GetBoolean());
        Assert.False(attributes.TryGetProperty("accountId", out _));
        Assert.False(attributes.TryGetProperty("displayName", out _));
        Assert.DoesNotContain(email, attributes.ToString());
        var stored = Assert.Single(await GrantsOfAsync(id));
        Assert.Equal(("ConsoleOperator", email), (stored["Kind"].AsString, stored["Email"].AsString));
        Assert.False(stored.Contains("AccountId"));
    }

    [Fact]
    public async Task The_account_that_is_assigned_has_to_exist()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);

        var response = await AssignAsync(root, new { eventId = id, accountId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("account-not-found", await ErrorCodeAsync(response));
        Assert.Empty(await GrantsOfAsync(id));
    }

    [Fact]
    public async Task A_Console_Operator_is_an_Event_and_one_way_of_naming_a_person_and_nothing_else()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);

        var extra = await AssignAsync(
            root,
            new
            {
                eventId = id,
                accountId = colleague.Id,
                tenantId = "anything",
            }
        );
        var nobody = await AssignAsync(root, new { eventId = id });
        var both = await AssignAsync(
            root,
            new
            {
                eventId = id,
                accountId = colleague.Id,
                email = colleague.Email,
            }
        );
        var notAnEmail = await AssignAsync(root, new { eventId = id, email = "not an email" });
        var notAnAccount = await AssignAsync(root, new { eventId = id, accountId = "not-a-guid" });
        var noEvent = await AssignAsync(root, new { eventId = "not-a-guid", accountId = colleague.Id });

        Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(extra));
        Assert.Equal(HttpStatusCode.BadRequest, nobody.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(nobody));
        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(both));
        Assert.Equal(HttpStatusCode.BadRequest, notAnEmail.StatusCode);
        Assert.Equal("invalid-email", await ErrorCodeAsync(notAnEmail));
        Assert.Equal(HttpStatusCode.BadRequest, notAnAccount.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noEvent.StatusCode);
        Assert.Equal("invalid-event", await ErrorCodeAsync(noEvent));
        Assert.Empty(await GrantsOfAsync(id));
    }

    [Fact]
    public async Task Only_Root_assigns_a_Console_Operator_and_not_a_Console_Operator_itself_nor_a_Tenant_Root_of_another_Tenant()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var rootElsewhere = await SignedInAsync(api, client, _mongo.ConnectionString, other, TenantRootOf(other));
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);

        foreach (var caller in new[] { consoleOperator, member, rootElsewhere })
        {
            var response = await AssignAsync(caller, new { eventId = id, accountId = caller.Id });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("not-root", await ErrorCodeAsync(response));
        }

        Assert.Single(await GrantsOfAsync(id)); // the one it had
    }

    [Fact]
    public async Task The_Developer_assigns_a_Console_Operator_of_an_Event_in_any_Tenant()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, home: null, isDeveloper: true);
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);

        var response = await AssignAsync(developer, new { eventId = id, accountId = colleague.Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("consoleOperator", (await CapabilitiesAsync(colleague, id)).GetProperty("level").GetString());
    }

    [Fact]
    public async Task A_Console_Operator_is_assigned_while_the_Event_is_Live_and_never_once_it_has_ended()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, null, NOW);

        var onLive = await AssignAsync(root, new { eventId = live, accountId = colleague.Id });
        var onHistoric = await AssignAsync(root, new { eventId = historic, accountId = colleague.Id });

        Assert.Equal(HttpStatusCode.Created, onLive.StatusCode);
        Assert.Equal("consoleOperator", (await CapabilitiesAsync(colleague, live)).GetProperty("level").GetString());
        Assert.Equal(HttpStatusCode.Conflict, onHistoric.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(onHistoric));
        Assert.Empty(await GrantsOfAsync(historic));
    }

    [Fact]
    public async Task Root_and_the_Console_Operators_of_an_Event_list_its_Console_Operators_and_nobody_else_does()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        await EventSeed.GrantAsync(_mongo.ConnectionString, tenant, live, "Operator", null, member.Email, member.Id); // an Operator is no Console Operator

        var byRoot = await ListAsync(root, live);
        var byTheDeveloper = await ListAsync(developer, live);
        var byTheConsoleOperator = await ListAsync(consoleOperator, live);
        var byAnOperator = await ListAsync(member, live);

        foreach (var response in new[] { byRoot, byTheDeveloper, byTheConsoleOperator })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var listed = Assert.Single(
                (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").EnumerateArray()
            );
            Assert.Equal(
                consoleOperator.Id.ToString(),
                listed.GetProperty("attributes").GetProperty("accountId").GetString()
            );
        }

        Assert.Equal(HttpStatusCode.Forbidden, byAnOperator.StatusCode);
        Assert.Equal("not-console-operator", await ErrorCodeAsync(byAnOperator));
    }

    [Fact]
    public async Task The_Console_Operators_of_an_Event_are_cut_into_pages_in_the_order_of_their_emails()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);
        var marker = Guid.NewGuid().ToString("N");
        var ids = new Dictionary<string, Guid>();
        foreach (var name in new[] { "ccc", "aaa", "bbb" }) // assigned out of the order of their emails
        {
            ids[name] = await GrantIdAsync(
                await AssignAsync(root, new { eventId = id, email = $"{name}.{marker}@sign-in.test" })
            );
        }

        var first = await PageOfAsync(root, id, "&page[size]=2");
        var last = await PageOfAsync(root, id, "&page[size]=2&page[number]=2");
        var backwards = await PageOfAsync(root, id, "&sort=-email&page[size]=2");
        var refused = await ListAsync(root, id, "&sort=name");

        Assert.Equal([ids["aaa"], ids["bbb"]], first);
        Assert.Equal([ids["ccc"]], last);
        Assert.Equal([ids["ccc"], ids["bbb"]], backwards);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task Root_removes_a_Console_Operator_and_it_no_longer_runs_the_Event()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        var grant = Assert.Single(await GrantsOfAsync(live))["_id"].AsGuid;

        var removed = await RemoveAsync(root, grant);
        var again = await RemoveAsync(root, grant);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Empty(await GrantsOfAsync(live));
        Assert.Equal("account", (await CapabilitiesAsync(consoleOperator, live)).GetProperty("level").GetString());
        Assert.Equal("root", (await CapabilitiesAsync(root, live)).GetProperty("level").GetString()); // a Live Event cannot be locked out
    }

    [Fact]
    public async Task A_Console_Operator_is_removed_by_Root_alone_and_is_not_there_for_anybody_who_may_not_see_it()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        await EventSeed.ConsoleOperatorAsync(_mongo.ConnectionString, tenant, live, colleague.Id);
        var grant = (await GrantsOfAsync(live)).First(x => x["AccountId"].AsGuid == colleague.Id)["_id"].AsGuid;

        var byAConsoleOperator = await RemoveAsync(consoleOperator, grant);
        var byAMember = await RemoveAsync(member, grant);
        var missing = await RemoveAsync(consoleOperator, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, byAConsoleOperator.StatusCode);
        Assert.Equal("not-root", await ErrorCodeAsync(byAConsoleOperator));
        Assert.Equal(HttpStatusCode.NotFound, byAMember.StatusCode); // it is not there, as a missing one is not
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(2, (await GrantsOfAsync(live)).Count);
    }

    [Fact]
    public async Task An_Operator_or_an_Official_grant_is_not_removed_as_a_Console_Operator()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, NOW);
        var grant = await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            live,
            "Operator",
            null,
            worker.Email,
            worker.Id
        );

        var response = await RemoveAsync(root, grant);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(await GrantsOfAsync(live));
    }

    [Fact]
    public async Task An_Event_that_is_not_there_is_not_found_by_anything_asked_of_it()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var missing = Guid.NewGuid();

        var assigned = await AssignAsync(root, new { eventId = missing, accountId = root.Id });
        var listed = await ListAsync(root, missing);
        var capabilities = await root.Page.GetAsync($"/api/events/{missing}/capabilities");

        Assert.Equal(HttpStatusCode.NotFound, assigned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, listed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, capabilities.StatusCode);
    }

    [Fact]
    public async Task The_clock_decides_when_an_Event_is_Live_and_when_it_is_not()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        var end = NOW.AddHours(6);
        await EventSeed.StartAsync(_mongo.ConnectionString, id, tenant, consoleOperator.Id, end);

        time.SetUtcNow(end.AddSeconds(-1));
        var oneSecondBefore = await CapabilitiesAsync(consoleOperator, id);
        var rootOneSecondBefore = await CapabilitiesAsync(root, id);
        time.SetUtcNow(end);
        var atTheEnd = await CapabilitiesAsync(consoleOperator, id);
        var rootAtTheEnd = await CapabilitiesAsync(root, id);

        Assert.Equal("live", oneSecondBefore.GetProperty("stage").GetString());
        Assert.True(oneSecondBefore.GetProperty("canRun").GetBoolean());
        Assert.True(rootOneSecondBefore.GetProperty("canAssignConsoleOperators").GetBoolean());
        Assert.Equal("historic", atTheEnd.GetProperty("stage").GetString());
        Assert.False(atTheEnd.GetProperty("canRun").GetBoolean());
        Assert.False(rootAtTheEnd.GetProperty("canAssignConsoleOperators").GetBoolean());
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await AssignAsync(root, new { eventId = id, accountId = colleague.Id })).StatusCode
        );
    }

    [Fact]
    public async Task What_the_capabilities_say_is_what_the_matrix_allows_the_caller_at_each_stage()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, other, isDeveloper: true);
        var rootElsewhere = await SignedInAsync(api, client, _mongo.ConnectionString, other, TenantRootOf(other));
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);

        // What the matrix of ADR-0012 allows each, by hand:
        // (level, canSnapshot, canConfigure, canEditSetup, canRun, canAssignConsoleOperators, canCreateEvents)
        await AssertCapabilitiesAsync(
            consoleOperator,
            unstarted,
            "consoleOperator",
            false,
            true,
            true,
            false,
            false,
            false
        );
        await AssertCapabilitiesAsync(consoleOperator, live, "consoleOperator", true, true, false, true, false, false);
        await AssertCapabilitiesAsync(
            consoleOperator,
            historic,
            "consoleOperator",
            false,
            false,
            false,
            false,
            false,
            false
        );
        await AssertCapabilitiesAsync(root, unstarted, "root", false, true, true, false, true, true);
        await AssertCapabilitiesAsync(root, live, "root", true, true, false, true, true, true);
        await AssertCapabilitiesAsync(root, historic, "root", false, false, false, false, false, true);
        await AssertCapabilitiesAsync(member, unstarted, "account", false, false, false, false, false, false);
        await AssertCapabilitiesAsync(member, live, "account", false, false, false, false, false, false);
        await AssertCapabilitiesAsync(developer, unstarted, "root", false, true, true, false, true, true);
        await AssertCapabilitiesAsync(developer, live, "root", true, true, false, true, true, true);
        await AssertCapabilitiesAsync(rootElsewhere, unstarted, "account", false, false, false, false, false, false);
        await AssertCapabilitiesAsync(rootElsewhere, live, "account", false, false, false, false, false, false);
    }

    static async Task AssertCapabilitiesAsync(
        Person person,
        Guid id,
        string level,
        bool canSnapshot,
        bool canConfigure,
        bool canEditSetup,
        bool canRun,
        bool canAssignConsoleOperators,
        bool canCreateEvents
    )
    {
        var capabilities = await CapabilitiesAsync(person, id);
        Assert.Equal(level, capabilities.GetProperty("level").GetString());
        Assert.Equal(canSnapshot, capabilities.GetProperty("canSnapshot").GetBoolean());
        Assert.Equal(canConfigure, capabilities.GetProperty("canConfigure").GetBoolean());
        Assert.Equal(canEditSetup, capabilities.GetProperty("canEditSetup").GetBoolean());
        Assert.Equal(canRun, capabilities.GetProperty("canRun").GetBoolean());
        Assert.Equal(canAssignConsoleOperators, capabilities.GetProperty("canAssignConsoleOperators").GetBoolean());
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

    /// <summary>The grants an Event has in the collection, whatever their kind, in the order they were made.</summary>
    async Task<List<BsonDocument>> GrantsOfAsync(Guid eventId)
    {
        return await EventSeed
            .Grants(_mongo.ConnectionString)
            .Find(new BsonDocument("EventId", EventSeed.Binary(eventId)))
            .Sort(new BsonDocument("_id", 1))
            .ToListAsync();
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

    static Task<HttpResponseMessage> AssignAsync(Person person, object attributes)
    {
        return person.Page.WriteAsync(HttpMethod.Post, "/api/console-operators", "console-operators", attributes);
    }

    static Task<HttpResponseMessage> RemoveAsync(Person person, Guid grant)
    {
        return person.Page.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/console-operators/{grant}"));
    }

    static Task<HttpResponseMessage> ListAsync(Person person, Guid eventId, string more = "")
    {
        return person.Page.GetAsync($"/api/console-operators?filter=eventId eq {eventId}{more}");
    }

    static async Task<List<Guid>> PageOfAsync(Person person, Guid eventId, string more)
    {
        var response = await ListAsync(person, eventId, more);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return
        [
            .. (await ApiSessions.ReadJsonAsync(response))
                .GetProperty("data")
                .EnumerateArray()
                .Select(x => Guid.Parse(x.GetProperty("id").GetString()!)),
        ];
    }

    static async Task<Guid> GrantIdAsync(HttpResponseMessage response)
    {
        return Guid.Parse(
            (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetString()!
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
