using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Harness;
using NTS.Tools.Developer;

namespace NTS.Tests.Integration;

/// <summary>
/// What an account is in the Tenants (ADR-0012, #643): the Memberships it holds and their roles, the Developer flag, the
/// Tenant it has selected, and the Tenant that all it reads and writes belongs to for now. Roles come from user
/// documents that only the Developer's command writes: no route gives one. A Tenant has rules for its Regional
/// competitions, which a Tenant Root edits.
/// </summary>
public sealed class TenantRolesTests : IClassFixture<SharedApiFixture>
{
    readonly SharedApiFixture _shared;

    public TenantRolesTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task A_new_account_has_a_Membership_in_its_home_Tenant_with_no_role_and_that_Tenant_is_the_current_one()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var home = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, home);

        var me = await MeAsync(person);

        Assert.False(me.GetProperty("isDeveloper").GetBoolean());
        Assert.Equal(home, me.GetProperty("homeTenantId").GetString());
        Assert.Equal(home, me.GetProperty("currentTenantId").GetString());
        Assert.False(me.TryGetProperty("selectedTenantId", out _));
        var membership = Assert.Single(me.GetProperty("memberships").EnumerateArray());
        Assert.Equal(home, membership.GetProperty("tenantId").GetString());
        Assert.Empty(membership.GetProperty("roles").EnumerateArray());
    }

    [Fact]
    public async Task A_Tenant_Root_seeded_by_the_command_shows_the_role_and_makes_the_Tenant_operational()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var before = await TenantAsync(person, tenant);

        await DeveloperCommands.SeedTenantRoot(
            new MongoClient(_shared.MongoConnectionString).GetDatabase(
                MongoLease.DatabaseNameOf(_shared.MongoConnectionString)
            ),
            tenant,
            person.Email,
            apply: true
        );

        var membership = Assert.Single((await MeAsync(person)).GetProperty("memberships").EnumerateArray());
        Assert.Equal(["tenant-root"], membership.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
        Assert.False(before.GetProperty("attributes").GetProperty("isOperational").GetBoolean());
        Assert.True(
            (await TenantAsync(person, tenant)).GetProperty("attributes").GetProperty("isOperational").GetBoolean()
        );
    }

    [Fact]
    public async Task The_Developer_granted_by_the_command_is_one_in_the_account_and_holds_no_Membership_for_it()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, tenant);

        await DeveloperCommands.GrantDeveloper(
            new MongoClient(_shared.MongoConnectionString).GetDatabase(
                MongoLease.DatabaseNameOf(_shared.MongoConnectionString)
            ),
            person.Email,
            apply: true
        );

        var me = await MeAsync(person);
        Assert.True(me.GetProperty("isDeveloper").GetBoolean());
        Assert.Single(me.GetProperty("memberships").EnumerateArray());
    }

    [Fact]
    public async Task An_account_selects_a_Tenant_it_is_a_member_of_and_the_selection_is_the_current_Tenant_until_it_is_cleared()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var home = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var other = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var person = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            home,
            TenancySeed.TenantRootOf(other)
        );

        var selected = await SelectAsync(person, other);
        var cleared = await SelectAsync(person, null);

        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        var afterSelecting = (await ApiSessions.ReadJsonAsync(selected)).GetProperty("data").GetProperty("attributes");
        Assert.Equal(other, afterSelecting.GetProperty("selectedTenantId").GetString());
        Assert.Equal(other, afterSelecting.GetProperty("currentTenantId").GetString());
        Assert.Equal(home, afterSelecting.GetProperty("homeTenantId").GetString());
        var afterClearing = (await ApiSessions.ReadJsonAsync(cleared)).GetProperty("data").GetProperty("attributes");
        Assert.False(afterClearing.TryGetProperty("selectedTenantId", out _));
        Assert.Equal(home, afterClearing.GetProperty("currentTenantId").GetString());
        Assert.Equal(home, afterClearing.GetProperty("homeTenantId").GetString());
    }

    [Fact]
    public async Task An_account_cannot_select_a_Tenant_it_is_not_a_member_of()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var home = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var stranger = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, home);

        var response = await SelectAsync(person, stranger);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("not-a-member", await ErrorCodeAsync(response));
        Assert.Equal(home, (await MeAsync(person)).GetProperty("currentTenantId").GetString());
    }

    [Fact]
    public async Task A_selection_of_a_Tenant_the_account_is_no_longer_a_member_of_does_not_count()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var home = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var gone = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var person = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            home,
            selectedTenant: gone
        );

        var me = await MeAsync(person);

        Assert.Equal(home, me.GetProperty("currentTenantId").GetString());
        Assert.False(me.TryGetProperty("selectedTenantId", out _));
    }

    [Fact]
    public async Task An_account_that_has_no_home_Tenant_has_no_current_Tenant()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var person = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, home: null);

        var me = await MeAsync(person);

        Assert.False(me.TryGetProperty("currentTenantId", out _));
        Assert.False(me.TryGetProperty("homeTenantId", out _));
        Assert.Empty(me.GetProperty("memberships").EnumerateArray());
    }

    [Theory]
    [InlineData("memberships")]
    [InlineData("roles")]
    [InlineData("isDeveloper")]
    [InlineData("homeTenantId")]
    public async Task No_route_of_the_account_gives_a_role_the_Developer_flag_or_another_home(string member)
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var home = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, home);
        var before = await StoredAsync(person.Email);
        var attributes = new Dictionary<string, object>
        {
            [member] = member == "isDeveloper" ? true : new[] { "tenant-root" },
        };

        var onTheAccount = await person.Page.WriteAsync(HttpMethod.Patch, "/api/me", "accounts", attributes);
        var onTheProfile = await person.Page.WriteAsync(HttpMethod.Patch, "/api/me/profile", "profiles", attributes);

        Assert.Equal(HttpStatusCode.BadRequest, onTheAccount.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(onTheAccount));
        Assert.Equal(before, await StoredAsync(person.Email)); // the profile edit may succeed and ignore it: nothing changed
        Assert.True(onTheProfile.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Any_signed_in_account_reads_a_Tenant_and_it_says_whether_the_Tenant_is_operational_and_its_rules()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(
            _shared.MongoConnectionString,
            "Bulgaria",
            new BsonDocument { { "OnlyAverageLoopSpeed", true }, { "RankerCode", "BG" } }
        );
        var person = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, home: null);

        var resource = await TenantAsync(person, tenant);

        Assert.Equal("tenants", resource.GetProperty("type").GetString());
        Assert.Equal(tenant, resource.GetProperty("id").GetString());
        var attributes = resource.GetProperty("attributes");
        Assert.Equal("Bulgaria", attributes.GetProperty("name").GetString());
        Assert.Equal("country", attributes.GetProperty("kind").GetString());
        Assert.False(attributes.GetProperty("isOperational").GetBoolean());
        Assert.True(attributes.GetProperty("regionalRules").GetProperty("onlyAverageLoopSpeed").GetBoolean());
        Assert.Equal("BG", attributes.GetProperty("regionalRules").GetProperty("rankerCode").GetString());
    }

    [Fact]
    public async Task A_Tenant_that_has_set_no_rules_has_the_FEIs_and_one_that_is_not_there_is_not_found()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, home: null);

        var rules = (await TenantAsync(person, tenant)).GetProperty("attributes").GetProperty("regionalRules");
        var missing = await person.Page.GetAsync("/api/tenants/country-nowhere");

        Assert.False(rules.GetProperty("onlyAverageLoopSpeed").GetBoolean());
        Assert.False(rules.TryGetProperty("rankerCode", out _));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not-found", await ErrorCodeAsync(missing));
    }

    [Fact]
    public async Task A_Tenant_Root_edits_the_rules_of_its_Tenant_and_the_edit_changes_only_what_it_names()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(
            _shared.MongoConnectionString,
            rules: new BsonDocument { { "OnlyAverageLoopSpeed", true }, { "RankerCode", "BG" } }
        );
        var root = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            tenant,
            TenancySeed.TenantRootOf(tenant)
        );

        var before = await StoredTenantAsync(tenant);

        var onlyTheRanker = await EditRulesAsync(root, tenant, new { rankerCode = "XX" });
        var onlyTheSpeed = await EditRulesAsync(root, tenant, new { onlyAverageLoopSpeed = false });
        var clearTheRanker = await EditRulesAsync(root, tenant, new { rankerCode = (string?)null });

        Assert.Equal(HttpStatusCode.OK, onlyTheRanker.StatusCode);
        var first = Rules(await ApiSessions.ReadJsonAsync(onlyTheRanker));
        Assert.True(first.GetProperty("onlyAverageLoopSpeed").GetBoolean());
        Assert.Equal("XX", first.GetProperty("rankerCode").GetString());
        var second = Rules(await ApiSessions.ReadJsonAsync(onlyTheSpeed));
        Assert.False(second.GetProperty("onlyAverageLoopSpeed").GetBoolean());
        Assert.Equal("XX", second.GetProperty("rankerCode").GetString());
        var third = Rules(await ApiSessions.ReadJsonAsync(clearTheRanker));
        Assert.False(third.GetProperty("onlyAverageLoopSpeed").GetBoolean());
        Assert.False(third.TryGetProperty("rankerCode", out _));
        var stored = await StoredTenantAsync(tenant);
        Assert.False(stored["RegionalRules"].AsBsonDocument["OnlyAverageLoopSpeed"].AsBoolean);
        stored.Remove("RegionalRules");
        before.Remove("RegionalRules");
        Assert.Equal(before, stored); // the name and the kind of the Tenant are as they were
    }

    [Fact]
    public async Task What_a_Tenant_keeps_among_its_rules_beyond_the_ones_the_Api_knows_survives_an_edit()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(
            _shared.MongoConnectionString,
            rules: new BsonDocument
            {
                { "OnlyAverageLoopSpeed", false },
                { "RankerCode", "BG" },
                { "Another", "kept" },
            }
        );
        var root = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            tenant,
            TenancySeed.TenantRootOf(tenant)
        );

        var speed = await EditRulesAsync(root, tenant, new { onlyAverageLoopSpeed = true });
        var ranker = await EditRulesAsync(root, tenant, new { rankerCode = (string?)null });

        Assert.Equal(HttpStatusCode.OK, speed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ranker.StatusCode);
        var rules = (await StoredTenantAsync(tenant))["RegionalRules"].AsBsonDocument;
        Assert.True(rules["OnlyAverageLoopSpeed"].AsBoolean);
        Assert.False(rules.Contains("RankerCode"));
        Assert.Equal("kept", rules["Another"].AsString);
    }

    [Fact]
    public async Task Two_edits_of_different_rules_that_started_from_the_same_rules_do_not_undo_each_other()
    {
        var store = new TenantStore(
            new MongoClient(_shared.MongoConnectionString),
            Options.Create(new NIdentityOptions { Database = MongoLease.DatabaseNameOf(_shared.MongoConnectionString) })
        );
        var started = RegionalRules.None;
        var speedFirst = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var rankerFirst = await TenancySeed.TenantAsync(_shared.MongoConnectionString);

        // Each was made from the rules as they were at the start, and each changes one member of them: in either order.
        var speed = await store.SetRulesAsync(speedFirst, started, new RegionalRules(true), CancellationToken.None);
        var ranker = await store.SetRulesAsync(
            speedFirst,
            started,
            new RegionalRules(false, "BG"),
            CancellationToken.None
        );
        var otherRanker = await store.SetRulesAsync(
            rankerFirst,
            started,
            new RegionalRules(false, "BG"),
            CancellationToken.None
        );
        var otherSpeed = await store.SetRulesAsync(
            rankerFirst,
            started,
            new RegionalRules(true),
            CancellationToken.None
        );
        var nothing = await store.SetRulesAsync(rankerFirst, started, started, CancellationToken.None);

        Assert.True(speed && ranker && otherRanker && otherSpeed);
        Assert.False(nothing);
        foreach (var tenant in new[] { speedFirst, rankerFirst })
        {
            var rules = (await StoredTenantAsync(tenant))["RegionalRules"].AsBsonDocument;
            Assert.True(rules["OnlyAverageLoopSpeed"].AsBoolean);
            Assert.Equal("BG", rules["RankerCode"].AsString);
        }
    }

    [Fact]
    public async Task Only_a_Tenant_Root_of_that_Tenant_or_the_Developer_edits_its_rules()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var other = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var member = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var rootElsewhere = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            other,
            TenancySeed.TenantRootOf(other)
        );
        var developer = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            other,
            isDeveloper: true
        );

        var byAMember = await EditRulesAsync(member, tenant, new { onlyAverageLoopSpeed = true });
        var byARootElsewhere = await EditRulesAsync(rootElsewhere, tenant, new { onlyAverageLoopSpeed = true });
        var byTheDeveloper = await EditRulesAsync(developer, tenant, new { onlyAverageLoopSpeed = true });

        Assert.Equal(HttpStatusCode.Forbidden, byAMember.StatusCode);
        Assert.Equal("not-root", await ErrorCodeAsync(byAMember));
        Assert.Equal(HttpStatusCode.Forbidden, byARootElsewhere.StatusCode);
        Assert.Equal("not-root", await ErrorCodeAsync(byARootElsewhere));
        Assert.Equal(HttpStatusCode.OK, byTheDeveloper.StatusCode);
    }

    [Fact]
    public async Task Nothing_of_a_Tenant_but_its_rules_is_edited_by_naming_it()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var root = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            tenant,
            TenancySeed.TenantRootOf(tenant)
        );
        var before = await StoredTenantAsync(tenant);

        var renamed = await root.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/tenants/{tenant}",
            "tenants",
            new
            {
                name = "Hacked",
                kind = "other",
                isOperational = false,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, renamed.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(renamed));
        Assert.Equal(before, await StoredTenantAsync(tenant));
    }

    [Theory]
    [InlineData("{\"onlyAverageLoopSpeed\":\"yes\"}")]
    [InlineData("{\"rankerCode\":5}")]
    [InlineData("{\"rankerCode\":\"\"}")]
    [InlineData("{\"rankerCode\":\"a\\nb\"}")]
    [InlineData("{\"unknown\":true}")]
    public async Task Rules_that_are_not_valid_are_refused_and_nothing_is_saved(string rules)
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var root = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            tenant,
            TenancySeed.TenantRootOf(tenant)
        );
        var before = await StoredTenantAsync(tenant);

        var response = await EditRulesAsync(root, tenant, JsonSerializer.Deserialize<JsonElement>(rules));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-rules", await ErrorCodeAsync(response));
        Assert.Equal(before, await StoredTenantAsync(tenant));
    }

    [Fact]
    public async Task What_a_person_may_do_in_a_Tenant_is_told_by_the_same_policy_that_refuses_it()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var member = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var root = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            tenant,
            TenancySeed.TenantRootOf(tenant)
        );
        var developer = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            tenant,
            isDeveloper: true
        );

        var ofMember = await CapabilitiesOfTenantAsync(member, tenant);
        var ofRoot = await CapabilitiesOfTenantAsync(root, tenant);
        var ofDeveloper = await CapabilitiesOfTenantAsync(developer, tenant);

        Assert.Equal("account", ofMember.GetProperty("level").GetString());
        Assert.All(MayOf(ofMember), x => Assert.False(x.Value.GetBoolean(), x.Name));
        Assert.Equal("root", ofRoot.GetProperty("level").GetString());
        Assert.True(ofRoot.GetProperty("canCreateEvents").GetBoolean());
        Assert.True(ofRoot.GetProperty("canEditTenant").GetBoolean());
        Assert.True(ofRoot.GetProperty("canEditRegistry").GetBoolean());
        Assert.True(ofDeveloper.GetProperty("canEditTenant").GetBoolean());
        Assert.True(ofDeveloper.GetProperty("canCreateEvents").GetBoolean()); // the Tenant has a Tenant Root, so it holds Events
    }

    [Fact]
    public async Task A_Tenant_that_has_no_Tenant_Root_holds_no_Events_whoever_asks()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var developer = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            tenant,
            isDeveloper: true
        );

        var capabilities = await CapabilitiesOfTenantAsync(developer, tenant);

        Assert.False(capabilities.GetProperty("canCreateEvents").GetBoolean());
        Assert.True(capabilities.GetProperty("canEditTenant").GetBoolean());
    }

    [Fact]
    public async Task The_Developer_selects_any_Tenant_that_exists_without_being_a_member_of_it_and_none_that_does_not()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var home = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var elsewhere = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var developer = await TenancySeed.SignedInAsync(
            api,
            client,
            _shared.MongoConnectionString,
            home,
            isDeveloper: true
        );

        var selected = await SelectAsync(developer, elsewhere);
        var nowhere = await SelectAsync(developer, "country-nowhere");

        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(selected)).GetProperty("data").GetProperty("attributes");
        Assert.Equal(elsewhere, attributes.GetProperty("currentTenantId").GetString());
        Assert.Single(attributes.GetProperty("memberships").EnumerateArray()); // acting in a Tenant makes no Membership
        Assert.Equal(HttpStatusCode.UnprocessableEntity, nowhere.StatusCode);
        Assert.Equal("not-a-member", await ErrorCodeAsync(nowhere));
    }

    [Fact]
    public async Task A_Console_Operator_of_an_Event_that_is_not_Historic_edits_the_registry_and_searches_the_accounts_of_the_Tenant()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_shared.MongoConnectionString);
        var consoleOperator = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var former = await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        await EventSeed.SetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id);
        await EventSeed.HistoricAsync(_shared.MongoConnectionString, tenant, former.Id, DateTimeOffset.UtcNow);

        var ofTheConsoleOperator = await CapabilitiesOfTenantAsync(consoleOperator, tenant);
        var ofTheFormerOne = await CapabilitiesOfTenantAsync(former, tenant);

        Assert.Equal("consoleOperator", ofTheConsoleOperator.GetProperty("level").GetString());
        Assert.True(ofTheConsoleOperator.GetProperty("canEditRegistry").GetBoolean());
        Assert.False(ofTheConsoleOperator.GetProperty("canEditTenant").GetBoolean());
        Assert.False(ofTheConsoleOperator.GetProperty("canCreateEvents").GetBoolean());
        Assert.Equal("account", ofTheFormerOne.GetProperty("level").GetString()); // the Event it ran is over
        Assert.All(MayOf(ofTheFormerOne), x => Assert.False(x.Value.GetBoolean(), x.Name));
    }

    /// <summary>What a person may do, which is every member of the capabilities that is a yes or a no.</summary>
    static IEnumerable<JsonProperty> MayOf(JsonElement capabilities)
    {
        return capabilities
            .EnumerateObject()
            .Where(x => x.Value.ValueKind is JsonValueKind.True or JsonValueKind.False);
    }

    static async Task<JsonElement> MeAsync(TenancySeed.Person person)
    {
        var response = await person.Page.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
    }

    static Task<HttpResponseMessage> SelectAsync(TenancySeed.Person person, string? tenant)
    {
        return person.Page.WriteAsync(HttpMethod.Patch, "/api/me", "accounts", new { selectedTenantId = tenant });
    }

    static async Task<JsonElement> TenantAsync(TenancySeed.Person person, string tenant)
    {
        var response = await person.Page.GetAsync($"/api/tenants/{tenant}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
    }

    static Task<HttpResponseMessage> EditRulesAsync(TenancySeed.Person person, string tenant, object rules)
    {
        return person.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/tenants/{tenant}",
            "tenants",
            new { regionalRules = rules }
        );
    }

    static JsonElement Rules(JsonElement body)
    {
        return body.GetProperty("data").GetProperty("attributes").GetProperty("regionalRules");
    }

    static async Task<JsonElement> CapabilitiesOfTenantAsync(TenancySeed.Person person, string tenant)
    {
        var response = await person.Page.GetAsync($"/api/tenants/{tenant}/capabilities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal("capabilities", resource.GetProperty("type").GetString());
        Assert.Equal(tenant, resource.GetProperty("id").GetString());
        return resource.GetProperty("attributes");
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    async Task<BsonDocument> StoredAsync(string email)
    {
        return await UserSeed.Users(_shared.MongoConnectionString).Find(new BsonDocument("Email", email)).FirstAsync();
    }

    async Task<BsonDocument> StoredTenantAsync(string tenant)
    {
        return await TenancySeed
            .Tenants(_shared.MongoConnectionString)
            .Find(new BsonDocument("_id", tenant))
            .FirstAsync();
    }
}
