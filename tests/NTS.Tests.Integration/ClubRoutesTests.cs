using System.Net;
using System.Text.Json;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Clubs of a Tenant's registry (#603, ADR-0008, ADR-0012) served by the Api as the rest-api skill prescribes. They are
/// the first family ported, so these tests also hold what every family of reference data does alike: who may read and who
/// may edit, the Tenant that owns a row and that no other Tenant sees, the id a client makes, what a document may name,
/// and the filter, the sort and the pages of a list.
/// </summary>
public sealed class ClubRoutesTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public ClubRoutesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Tenant_Root_makes_a_club_in_its_Tenant_with_the_id_the_document_names()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = Guid.NewGuid();

        var response = await CreateAsync(root, "Sofia Riders", id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/clubs/{id}", response.Headers.Location?.ToString());
        var resource = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal("clubs", resource.GetProperty("type").GetString());
        Assert.Equal(id.ToString(), resource.GetProperty("id").GetString());
        Assert.Equal("Sofia Riders", resource.GetProperty("attributes").GetProperty("name").GetString());
        Assert.Equal(tenant, resource.GetProperty("attributes").GetProperty("tenantId").GetString());
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id))!;
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal("Sofia Riders", stored["Name"].AsString);
    }

    [Fact]
    public async Task The_server_makes_the_id_of_a_club_whose_document_names_none()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        var response = await CreateAsync(root, "Plovdiv Riders");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = Guid.Parse(
            (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetString()!
        );
        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id));
    }

    [Fact]
    public async Task A_club_made_again_with_the_same_id_is_the_one_that_was_made_and_nothing_is_put_over_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = Guid.NewGuid();
        await CreateAsync(root, "First", id);

        var again = await CreateAsync(root, "Second", id);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(again)).GetProperty("data");
        Assert.Equal("First", resource.GetProperty("attributes").GetProperty("name").GetString());
        Assert.Equal("First", (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id))!["Name"].AsString);
    }

    [Fact]
    public async Task An_id_that_is_another_Tenants_is_not_given_away_by_making_a_club_with_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var theirs = await RegistrySeed.ClubAsync(_mongo.ConnectionString, other, "Theirs");

        var response = await CreateAsync(root, "Mine", theirs);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("id-taken", await ErrorCodeAsync(response));
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", theirs))!;
        Assert.Equal(other, stored["TenantId"].AsString);
        Assert.Equal("Theirs", stored["Name"].AsString);
    }

    [Fact]
    public async Task Whoever_the_matrix_lets_edit_the_registry_makes_a_club_and_nobody_else_does()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var historicOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var rootElsewhere = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(other));
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, historicOperator.Id, DateTimeOffset.UtcNow);

        foreach (var who in new[] { root, mainOperator, developer })
        {
            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(who, "Allowed")).StatusCode);
        }

        foreach (var who in new[] { member, historicOperator, rootElsewhere })
        {
            var response = await CreateAsync(who, "Refused");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("not-allowed", await ErrorCodeAsync(response));
        }

        Assert.Equal(3, await RegistrySeed.CountAsync(_mongo.ConnectionString, "clubs", tenant));
    }

    [Fact]
    public async Task A_caller_who_is_not_signed_in_reads_and_writes_nothing()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var id = Guid.NewGuid();

        var list = await client.GetAsync("/api/clubs");
        var item = await client.GetAsync($"/api/clubs/{id}");

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal("not-signed-in", await ErrorCodeAsync(list));
        Assert.Equal(HttpStatusCode.Unauthorized, item.StatusCode);
    }

    [Fact]
    public async Task Any_signed_in_account_reads_a_club_of_its_Tenant_and_one_of_another_is_not_found()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var mine = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Mine");
        var theirs = await RegistrySeed.ClubAsync(_mongo.ConnectionString, other, "Theirs");

        var found = await member.Page.GetAsync($"/api/clubs/{mine}");
        var foreign = await member.Page.GetAsync($"/api/clubs/{theirs}");
        var missing = await member.Page.GetAsync($"/api/clubs/{Guid.NewGuid()}");
        var malformed = await member.Page.GetAsync("/api/clubs/not-a-guid");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(found)).GetProperty("data");
        Assert.Equal(mine.ToString(), resource.GetProperty("id").GetString());
        Assert.Equal("Mine", resource.GetProperty("attributes").GetProperty("name").GetString());
        foreach (var response in new[] { foreign, missing, malformed })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("not-found", await ErrorCodeAsync(response));
        }
    }

    [Fact]
    public async Task The_clubs_listed_are_the_ones_of_the_current_Tenant_and_an_account_with_none_gets_an_empty_list()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var stateless = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Alpha");
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Beta");
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, other, "Gamma");

        var mine = await ListAsync(member);
        var none = await ListAsync(stateless);

        Assert.Equal(["Alpha", "Beta"], Names(mine).Order());
        Assert.Empty(none.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task An_account_with_no_current_Tenant_writes_nothing_to_the_registry_and_is_told_why()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var stateless = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var id = Guid.NewGuid();

        var made = await CreateAsync(stateless, "Nowhere", id);
        var changed = await stateless.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/clubs/{id}",
            "clubs",
            new { name = "Somewhere" }
        );
        var removed = await stateless.Page.DeleteAsync($"/api/clubs/{id}");

        foreach (var response in new[] { made, changed, removed })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("no-current-tenant", await ErrorCodeAsync(response));
        }

        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id));
    }

    [Fact]
    public async Task A_list_is_filtered_by_an_expression_of_the_OData_grammar_over_the_clubs_of_the_Tenant_only()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var alpha = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Alpha Riders");
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Beta Riders");
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Gamma Club");
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "O'Brien Club");
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, other, "Alpha Foreign");

        Assert.Equal(["Beta Riders"], Names(await ListAsync(member, "?filter=name eq 'Beta Riders'")));
        Assert.Equal(
            ["Alpha Riders", "Beta Riders"],
            Names(await ListAsync(member, "?filter=contains(name,'Riders')")).Order()
        );
        Assert.Equal(["Gamma Club"], Names(await ListAsync(member, "?filter=startswith(name,'Gamma')")));
        Assert.Equal(
            ["Alpha Riders", "Gamma Club"],
            Names(await ListAsync(member, "?filter=name eq 'Alpha Riders' or name eq 'Gamma Club'")).Order()
        );
        Assert.Equal(["Alpha Riders"], Names(await ListAsync(member, $"?filter=id eq {alpha}")));
        Assert.Equal(
            ["Beta Riders"],
            Names(await ListAsync(member, "?filter=contains(name,'Riders') and not contains(name,'Alpha')"))
        );
        Assert.Equal(
            ["Gamma Club", "O'Brien Club"],
            Names(await ListAsync(member, "?filter=not contains(name,'Riders')")).Order()
        );
        Assert.Equal(["Alpha Riders"], Names(await ListAsync(member, "?filter=name lt 'B'")));
        Assert.Equal(["Gamma Club"], Names(await ListAsync(member, "?filter=tolower(name) eq 'gamma club'")));
        Assert.Equal(["O'Brien Club"], Names(await ListAsync(member, "?filter=name eq 'O''Brien Club'")));
        Assert.Empty(Names(await ListAsync(member, $"?filter=tenantId eq '{other}'")));
        Assert.Empty(Names(await ListAsync(member, "?filter=name eq 'Alpha Foreign'")));
    }

    [Fact]
    public async Task A_list_that_names_no_page_is_the_first_hundred_rows_and_the_next_page_is_the_rest()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await RegistrySeed.ClubsAsync(_mongo.ConnectionString, tenant, 130, "Club");

        var first = await ListAsync(member);
        var next = first.GetProperty("links").GetProperty("next").GetString()!;
        var second = await ListAsync(member, next["/api/clubs".Length..]);

        Assert.Equal(100, first.GetProperty("data").GetArrayLength());
        Assert.Equal(100, first.GetProperty("meta").GetProperty("page").GetProperty("size").GetInt32());
        Assert.Equal(30, second.GetProperty("data").GetArrayLength());
        Assert.False(second.GetProperty("links").TryGetProperty("next", out _));
        Assert.Empty(
            first
                .GetProperty("data")
                .EnumerateArray()
                .Select(x => x.GetProperty("id").GetString())
                .Intersect(second.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetString()))
        );
    }

    [Theory]
    [InlineData("name eq")]
    [InlineData("nope eq 'x'")]
    [InlineData("name eq 'x' and")]
    [InlineData("")]
    [InlineData("contains(name)")]
    public async Task A_filter_that_is_not_valid_is_refused_and_nothing_is_listed(string filter)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);

        var response = await member.Page.GetAsync("/api/clubs?filter=" + Uri.EscapeDataString(filter));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-filter", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_list_is_sorted_by_the_members_asked_for_and_a_member_with_a_minus_goes_down()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        foreach (var name in new[] { "Beta", "Alpha", "Gamma" })
        {
            await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, name);
        }

        Assert.Equal(["Alpha", "Beta", "Gamma"], Names(await ListAsync(member, "?sort=name")));
        Assert.Equal(["Gamma", "Beta", "Alpha"], Names(await ListAsync(member, "?sort=-name")));
        Assert.Equal(
            ["Gamma"],
            Names(await ListAsync(member, "?sort=-name&filter=name ne 'Alpha' and name ne 'Beta'"))
        );
    }

    [Theory]
    [InlineData("-")]
    [InlineData("na me")]
    [InlineData("name;drop")]
    [InlineData("nope")]
    public async Task A_sort_that_is_not_a_list_of_members_of_the_resource_is_refused(string sort)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);

        var response = await member.Page.GetAsync("/api/clubs?sort=" + Uri.EscapeDataString(sort));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-sort", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_list_is_cut_into_pages_that_do_not_overlap_and_say_where_the_next_one_is()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        for (var i = 0; i < 5; i++)
        {
            await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, $"Club {i}");
        }

        var first = await ListAsync(member, "?page[size]=2&page[number]=1");
        var second = await ListAsync(member, "?page[size]=2&page[number]=2");
        var third = await ListAsync(member, "?page[size]=2&page[number]=3");
        var beyond = await ListAsync(member, "?page[size]=2&page[number]=4");

        Assert.Equal(2, Names(first).Count());
        Assert.Equal(2, Names(second).Count());
        Assert.Single(Names(third));
        Assert.Empty(Names(beyond));
        Assert.Equal(5, Names(first).Concat(Names(second)).Concat(Names(third)).Distinct().Count());
        var next = first.GetProperty("links").GetProperty("next").GetString();
        Assert.Contains("page%5Bnumber%5D=2", next);
        Assert.Contains("page%5Bsize%5D=2", next);
        Assert.NotNull(second.GetProperty("links").GetProperty("next").GetString());
        Assert.False(third.GetProperty("links").TryGetProperty("next", out _));
        Assert.Equal(2, first.GetProperty("meta").GetProperty("page").GetProperty("size").GetInt32());
        Assert.Equal(3, third.GetProperty("meta").GetProperty("page").GetProperty("number").GetInt32());
    }

    [Theory]
    [InlineData("page[size]=0")]
    [InlineData("page[size]=501")]
    [InlineData("page[size]=many")]
    [InlineData("page[number]=0")]
    [InlineData("page[number]=-1")]
    public async Task A_page_that_is_not_a_size_of_one_to_five_hundred_and_a_number_from_one_is_refused(string page)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);

        var response = await member.Page.GetAsync("/api/clubs?" + page);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-page", await ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData("include=anything")]
    [InlineData("$filter=name eq 'x'")]
    [InlineData("fields[clubs]=name")]
    [InlineData("allTenants=true")]
    public async Task Any_other_parameter_of_a_list_is_refused_and_none_widens_it_to_another_Tenant(string parameter)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);

        var response = await member.Page.GetAsync("/api/clubs?" + parameter);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("unsupported-parameter", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_club_is_changed_in_the_members_the_document_names_and_in_no_other()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Old Name");

        var response = await ChangeAsync(root, id, new { name = "New Name" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal(id.ToString(), resource.GetProperty("id").GetString());
        Assert.Equal("New Name", resource.GetProperty("attributes").GetProperty("name").GetString());
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id))!;
        Assert.Equal("New Name", stored["Name"].AsString);
        Assert.Equal(tenant, stored["TenantId"].AsString);
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("id")]
    [InlineData("bogus")]
    public async Task A_document_that_names_what_the_server_owns_or_what_there_is_not_is_refused_and_changes_nothing(
        string member
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Old Name");

        var change = await ChangeAsync(
            root,
            id,
            new Dictionary<string, object> { ["name"] = "New Name", [member] = "x" }
        );
        var create = await root.Page.WriteAsync(
            HttpMethod.Post,
            "/api/clubs",
            "clubs",
            new Dictionary<string, object> { ["name"] = "Made", [member] = "x" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(change));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(create));
        Assert.Equal(
            "Old Name",
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id))!["Name"].AsString
        );
        Assert.Equal(1, await RegistrySeed.CountAsync(_mongo.ConnectionString, "clubs", tenant));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_name_the_domain_does_not_accept_is_422_and_nothing_is_written(string name)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Old Name");

        var change = await ChangeAsync(root, id, new { name });
        var create = await CreateAsync(root, name);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, change.StatusCode);
        Assert.Equal("invalid-attribute", await ErrorCodeAsync(change));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, create.StatusCode);
        Assert.Equal(
            "Old Name",
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id))!["Name"].AsString
        );
        Assert.Equal(1, await RegistrySeed.CountAsync(_mongo.ConnectionString, "clubs", tenant));
    }

    [Fact]
    public async Task A_change_names_the_row_it_is_about_by_the_route_and_a_document_of_another_id_is_a_conflict()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Old Name");

        var response = await ChangeAsync(root, id, new { name = "New Name" }, bodyId: Guid.NewGuid());
        var withTheSameId = await ChangeAsync(root, id, new { name = "Same Id" }, bodyId: id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("id-mismatch", await ErrorCodeAsync(response));
        Assert.Equal(HttpStatusCode.OK, withTheSameId.StatusCode);
    }

    [Fact]
    public async Task A_club_of_another_Tenant_is_not_changed_or_removed_by_anybody_who_may_edit_the_registry_of_this_one()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var theirs = await RegistrySeed.ClubAsync(_mongo.ConnectionString, other, "Theirs");

        var change = await ChangeAsync(root, theirs, new { name = "Taken" });
        var remove = await root.Page.DeleteAsync($"/api/clubs/{theirs}");

        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", theirs))!;
        Assert.Equal("Theirs", stored["Name"].AsString);
        Assert.Equal(other, stored["TenantId"].AsString);
    }

    [Fact]
    public async Task Only_whoever_may_edit_the_registry_changes_or_removes_a_club()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Kept");

        var change = await ChangeAsync(member, id, new { name = "Taken" });
        var remove = await member.Page.DeleteAsync($"/api/clubs/{id}");
        var anonymous = await client.DeleteAsync($"/api/clubs/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, change.StatusCode);
        Assert.Equal("not-allowed", await ErrorCodeAsync(change));
        Assert.Equal(HttpStatusCode.Forbidden, remove.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("Kept", (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id))!["Name"].AsString);
    }

    [Fact]
    public async Task A_club_is_removed_and_is_then_not_found_and_one_that_is_not_there_is_not_found_either()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Gone");

        var removed = await root.Page.DeleteAsync($"/api/clubs/{id}");
        var again = await root.Page.DeleteAsync($"/api/clubs/{id}");
        var malformed = await root.Page.DeleteAsync("/api/clubs/not-a-guid");
        var read = await root.Page.GetAsync($"/api/clubs/{id}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id));
    }

    [Fact]
    public async Task A_write_needs_the_json_api_media_type_and_a_document_of_one_club()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        var wrongType = await root.Page.WriteAsync(HttpMethod.Post, "/api/clubs", "horses", new { name = "x" });
        using var plain = new HttpRequestMessage(HttpMethod.Post, "/api/clubs")
        {
            Content = new StringContent("{\"data\":{\"type\":\"clubs\",\"attributes\":{\"name\":\"x\"}}}"),
        };
        var json = await root.Page.SendAsync(plain);
        var badId = await root.Page.WriteAsync(HttpMethod.Post, "/api/clubs", "clubs", new { name = "x" }, id: "abc");

        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(wrongType));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, json.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badId.StatusCode);
        Assert.Equal("invalid-id", await ErrorCodeAsync(badId));
        Assert.Equal(0, await RegistrySeed.CountAsync(_mongo.ConnectionString, "clubs", tenant));
    }

    static Task<HttpResponseMessage> ChangeAsync(Person person, Guid id, object attributes, Guid? bodyId = null)
    {
        return person.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/clubs/{id}",
            "clubs",
            attributes,
            id: bodyId?.ToString()
        );
    }

    static Task<HttpResponseMessage> CreateAsync(Person person, string name, Guid? id = null)
    {
        return person.Page.WriteAsync(HttpMethod.Post, "/api/clubs", "clubs", new { name }, id: id?.ToString());
    }

    static async Task<JsonElement> ListAsync(Person person, string query = "")
    {
        var response = await person.Page.GetAsync("/api/clubs" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiSessions.ReadJsonAsync(response);
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
