using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.Tenancy;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The search of accounts by name (ADR-0012, #643) is how a Tenant Root or a Main Operator finds the person to link or
/// to assign, in place of a listing of everybody. It takes words of a name, one of at least two characters, answers a page
/// (ten by default) of accounts in the order of their names with a display name and an email that is partly masked, is
/// limited in how often it can be asked, a page being a search, and finds
/// the accounts of the Tenant it is asked in. It reaches across Tenants only through a route of its own, which the caller
/// has to ask for by name: no parameter does it.
/// </summary>
public sealed class AccountSearchTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public AccountSearchTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Tenant_Root_finds_the_accounts_of_its_Tenant_by_part_of_a_name_in_any_case()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var marker = Guid.NewGuid().ToString("N")[..6];
        var ours = await AccountAsync(_mongo.ConnectionString, tenant, name: $"Zlatina{marker} Georgieva");
        await AccountAsync(_mongo.ConnectionString, other, name: $"Zlatina{marker} Petrova");
        await AccountAsync(_mongo.ConnectionString, tenant, name: $"Other{marker} Person");

        var found = await SearchAsync(root, $"ZLATINA{marker}");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var items = (await ApiSessions.ReadJsonAsync(found)).GetProperty("data").EnumerateArray().ToList();
        var match = Assert.Single(items);
        Assert.Equal("account-matches", match.GetProperty("type").GetString());
        Assert.Equal(ours.Id.ToString(), match.GetProperty("id").GetString());
        var attributes = match.GetProperty("attributes");
        Assert.Equal($"Zlatina{marker} Georgieva", attributes.GetProperty("displayName").GetString());
        Assert.StartsWith(ours.Email[..1] + "***@", attributes.GetProperty("email").GetString());
        Assert.EndsWith(ours.Email[(ours.Email.IndexOf('@') + 1)..], attributes.GetProperty("email").GetString());
    }

    [Fact]
    public async Task The_accounts_of_other_Tenants_are_found_only_by_the_route_that_asks_for_them_by_name()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var marker = Guid.NewGuid().ToString("N")[..6];
        var ours = await AccountAsync(_mongo.ConnectionString, tenant, name: $"Kalina{marker} Ivanova");
        var theirs = await AccountAsync(_mongo.ConnectionString, other, name: $"Kalina{marker} Stoyanova");

        var here = await ApiSessions.ReadJsonAsync(await SearchAsync(root, $"kalina{marker}"));
        var everywhere = await ApiSessions.ReadJsonAsync(
            await SearchAsync(root, $"kalina{marker}", acrossTenants: true)
        );

        Assert.Equal(
            [ours.Id.ToString()],
            here.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetString())
        );
        Assert.Equal(
            new[] { ours.Id.ToString(), theirs.Id.ToString() }.Order(),
            everywhere.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetString()!).Order()
        );
    }

    [Fact]
    public async Task No_parameter_widens_a_search_and_no_member_but_the_name_is_searched()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        foreach (var path in new[] { "/api/accounts", "/api/accounts/all-tenants" })
        {
            var tenantParameter = await root.Page.GetAsync($"{path}?filter=contains(name,'abc')&tenantId=other");
            var allParameter = await root.Page.GetAsync($"{path}?filter=contains(name,'abc')&allTenants=true");
            var byEmail = await root.Page.GetAsync($"{path}?filter=contains(email,'abc')");
            var equality = await root.Page.GetAsync($"{path}?filter=name eq 'abc'");
            var noFilter = await root.Page.GetAsync(path);

            Assert.Equal("unsupported-parameter", await ErrorCodeAsync(tenantParameter));
            Assert.Equal("unsupported-parameter", await ErrorCodeAsync(allParameter));
            Assert.Equal("invalid-filter", await ErrorCodeAsync(byEmail));
            Assert.Equal("invalid-filter", await ErrorCodeAsync(equality));
            Assert.Equal("invalid-filter", await ErrorCodeAsync(noFilter));
        }
    }

    [Theory]
    [InlineData("a")]
    [InlineData("  a  ")]
    [InlineData("")]
    public async Task A_search_takes_at_least_two_characters(string text)
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        var response = await SearchAsync(root, text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("search-too-short", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task At_most_ten_accounts_are_answered_in_the_order_of_their_names()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var marker = Guid.NewGuid().ToString("N")[..6];
        for (var i = 0; i < 14; i++)
        {
            await AccountAsync(_mongo.ConnectionString, tenant, name: $"Many{marker} Person {i:00}");
        }

        var response = await SearchAsync(root, $"many{marker}");

        var names = (await ApiSessions.ReadJsonAsync(response))
            .GetProperty("data")
            .EnumerateArray()
            .Select(x => x.GetProperty("attributes").GetProperty("displayName").GetString()!)
            .ToList();
        Assert.Equal(10, names.Count);
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_accounts_are_answered_a_page_at_a_time_in_the_order_of_their_names_or_the_other_way_round(
        bool acrossTenants
    )
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var marker = Guid.NewGuid().ToString("N")[..6];
        for (var i = 13; i >= 0; i--)
        {
            await AccountAsync(_mongo.ConnectionString, tenant, name: $"Many{marker} Person {i:00}");
        }

        var first = await ReadAsync(await SearchAsync(root, $"many{marker}", acrossTenants));
        var second = await ReadAsync(await root.Page.GetAsync(first.Next!));
        var backwards = await ReadAsync(await SearchAsync(root, $"many{marker}", acrossTenants, "&sort=-name"));
        var small = await ReadAsync(
            await SearchAsync(root, $"many{marker}", acrossTenants, "&page%5Bsize%5D=3&page%5Bnumber%5D=5")
        );
        var full = await ReadAsync(
            await SearchAsync(root, $"many{marker}", acrossTenants, "&page%5Bsize%5D=7&page%5Bnumber%5D=2")
        );

        Assert.Equal(Enumerable.Range(7, 7).Select(i => $"Many{marker} Person {i:00}"), full.Names);
        Assert.Null(full.Next); // the page is full, and nothing follows
        Assert.Equal(Enumerable.Range(0, 10).Select(i => $"Many{marker} Person {i:00}"), first.Names);
        Assert.Equal((10, 1), (first.Size, first.Number));
        Assert.Equal(Enumerable.Range(10, 4).Select(i => $"Many{marker} Person {i:00}"), second.Names);
        Assert.Equal((10, 2), (second.Size, second.Number));
        Assert.Null(second.Next);
        Assert.Equal(Enumerable.Range(4, 10).Reverse().Select(i => $"Many{marker} Person {i:00}"), backwards.Names);
        Assert.Equal([$"Many{marker} Person 12", $"Many{marker} Person 13"], small.Names);
        Assert.Null(small.Next);
    }

    [Theory]
    [InlineData("&sort=id")]
    [InlineData("&sort=name,id")]
    [InlineData("&page%5Bsize%5D=26")]
    [InlineData("&page%5Bnumber%5D=0")]
    public async Task A_sort_or_a_page_that_the_search_does_not_take_is_refused(string parameters)
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        foreach (var acrossTenants in new[] { false, true })
        {
            var response = await SearchAsync(root, "abc", acrossTenants, parameters);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.StartsWith("invalid-", (await ErrorCodeAsync(response))!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_search_itself_refuses_words_that_are_too_short_whatever_the_route_did()
    {
        var search = new AccountSearch(
            new MongoClient(_mongo.ConnectionString),
            Options.Create(new NIdentityOptions { Database = UserSeed.DATABASE })
        );

        await Assert.ThrowsAsync<ArgumentException>(
            () => search.SearchAsync(new NameSearch(["a", "b"], descending: false, skip: 0, take: 11), null, default)
        );
    }

    [Fact]
    public async Task Every_word_of_the_text_is_part_of_the_name_in_any_order_and_in_any_case()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var marker = Guid.NewGuid().ToString("N")[..6];
        var ours = await AccountAsync(_mongo.ConnectionString, tenant, name: $"Zlatina{marker} Georgieva");
        await AccountAsync(_mongo.ConnectionString, tenant, name: $"Zlatina{marker} Petrova");

        var found = await ReadAsync(await SearchAsync(root, $"GEORGIEVA zlatina{marker}"));
        var nobody = await ReadAsync(await SearchAsync(root, $"georgieva zlatina{marker} petrova"));

        Assert.Equal([ours.Id.ToString()], found.Ids);
        Assert.Empty(nobody.Ids);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a b c d")]
    public async Task A_text_with_no_word_of_two_characters_is_too_short_however_long_it_is(string text)
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        var response = await SearchAsync(root, text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("search-too-short", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task What_is_asked_for_is_a_name_and_not_a_pattern()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        await AccountAsync(_mongo.ConnectionString, tenant, name: "Pattern Person");

        var everything = await ApiSessions.ReadJsonAsync(await SearchAsync(root, ".*."));
        var brackets = await SearchAsync(root, "(((");

        Assert.Empty(everything.GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, brackets.StatusCode);
    }

    [Fact]
    public async Task A_quote_in_what_is_asked_for_is_part_of_the_name()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var marker = Guid.NewGuid().ToString("N")[..6];
        await AccountAsync(_mongo.ConnectionString, tenant, name: $"D'Arcy{marker} Smith");

        var found = await ApiSessions.ReadJsonAsync(await SearchAsync(root, $"d'arcy{marker}"));

        Assert.Single(found.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task No_answer_holds_an_email_that_is_not_masked_or_anything_an_account_keeps_for_signing_in()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var marker = Guid.NewGuid().ToString("N")[..6];
        var people = new List<Seeded>();
        for (var i = 0; i < 3; i++)
        {
            people.Add(await AccountAsync(_mongo.ConnectionString, tenant, name: $"Leaky{marker} Person {i}"));
        }

        var here = await (await SearchAsync(root, $"leaky{marker}")).Content.ReadAsStringAsync();
        var everywhere = await (
            await SearchAsync(root, $"leaky{marker}", acrossTenants: true)
        ).Content.ReadAsStringAsync();

        foreach (var text in new[] { here, everywhere })
        {
            Assert.All(people, x => Assert.DoesNotContain(x.Email, text));
            Assert.DoesNotContain("SecurityStamp", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("passkey", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lockout", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("memberships", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Only_a_Tenant_Root_a_Main_Operator_of_an_Event_that_is_not_Historic_or_the_Developer_searches()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var formerMainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var rootElsewhere = await SignedInAsync(api, client, _mongo.ConnectionString, other, TenantRootOf(other));
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, formerMainOperator.Id, DateTimeOffset.UtcNow);

        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(root, "abc")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(mainOperator, "abc")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(developer, "abc")).StatusCode);
        foreach (var refused in new[] { member, formerMainOperator })
        {
            var response = await SearchAsync(refused, "abc");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("not-allowed", await ErrorCodeAsync(response));
        }

        // A Tenant Root of another Tenant has authority there, and searches the Tenant it is in as itself: its own.
        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(rootElsewhere, "abc")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SearchAsync(member, "abc", acrossTenants: true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(mainOperator, "abc", acrossTenants: true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(rootElsewhere, "abc", acrossTenants: true)).StatusCode);
    }

    [Fact]
    public async Task An_account_that_has_no_current_Tenant_finds_nobody_in_it_and_across_Tenants_only_by_authority()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, home: null, isDeveloper: true);
        var marker = Guid.NewGuid().ToString("N")[..6];
        await AccountAsync(_mongo.ConnectionString, tenant, name: $"Nowhere{marker} Person");

        var here = await ApiSessions.ReadJsonAsync(await SearchAsync(developer, $"nowhere{marker}"));
        var everywhere = await ApiSessions.ReadJsonAsync(
            await SearchAsync(developer, $"nowhere{marker}", acrossTenants: true)
        );

        Assert.Empty(here.GetProperty("data").EnumerateArray());
        Assert.Single(everywhere.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task How_often_a_person_may_search_is_limited_and_the_refusal_says_when_to_try_again()
    {
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            configureHost: builder =>
            {
                builder.UseSetting("Search:RateLimits:PerAccount", "3");
                builder.UseSetting("Search:RateLimits:Window", "00:10:00");
            }
        );
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var other = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        var allowed = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            allowed.Add((await SearchAsync(root, "abc")).StatusCode);
        }

        var refused = await SearchAsync(root, "abc");
        var someoneElse = await SearchAsync(other, "abc");

        Assert.All(allowed, x => Assert.Equal(HttpStatusCode.OK, x));
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("rate-limited", await ErrorCodeAsync(refused));
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(HttpStatusCode.OK, someoneElse.StatusCode);
    }

    [Fact]
    public async Task The_limit_is_for_the_whole_platform_too_and_a_window_that_ends_gives_room_again()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            time: time,
            configureHost: builder =>
            {
                builder.UseSetting("Search:RateLimits:Overall", "2");
                builder.UseSetting("Search:RateLimits:Window", "00:10:00");
            }
        );
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var first = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var second = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(first, "abc")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(second, "abc")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SearchAsync(first, "abc")).StatusCode);
        time.Advance(TimeSpan.FromMinutes(11));

        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(first, "abc")).StatusCode);
    }

    [Fact]
    public async Task A_search_that_is_refused_for_what_it_asks_costs_nothing_of_the_limit_and_one_for_its_role_costs_nothing_either()
    {
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            configureHost: builder => builder.UseSetting("Search:RateLimits:PerAccount", "2")
        );
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await SearchAsync(root, "a")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(root, "ab")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(root, "ab")).StatusCode);
    }

    ApiFactory NewApi()
    {
        return new ApiFactory(_mongo.ConnectionString);
    }

    static Task<HttpResponseMessage> SearchAsync(
        Person person,
        string text,
        bool acrossTenants = false,
        string parameters = ""
    )
    {
        var filter = Uri.EscapeDataString($"contains(name,'{text.Replace("'", "''")}')");
        return person.Page.GetAsync(
            $"/api/accounts{(acrossTenants ? "/all-tenants" : string.Empty)}?filter={filter}{parameters}"
        );
    }

    /// <summary>What a page of a search says: its rows, which page it is, and where the next one is when there is one.</summary>
    static async Task<Found> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiSessions.ReadJsonAsync(response);
        var rows = body.GetProperty("data").EnumerateArray().ToList();
        var page = body.GetProperty("meta").GetProperty("page");
        var links = body.GetProperty("links");
        return new Found(
            [.. rows.Select(x => x.GetProperty("attributes").GetProperty("displayName").GetString()!)],
            [.. rows.Select(x => x.GetProperty("id").GetString()!)],
            page.GetProperty("size").GetInt32(),
            page.GetProperty("number").GetInt32(),
            links.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null
        );
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    sealed class Found
    {
        public Found(List<string> names, List<string> ids, int size, int number, string? next)
        {
            Names = names;
            Ids = ids;
            Size = size;
            Number = number;
            Next = next;
        }

        public List<string> Names { get; }
        public List<string> Ids { get; }
        public int Size { get; }
        public int Number { get; }
        public string? Next { get; }
    }
}
