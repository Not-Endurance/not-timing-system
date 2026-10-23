using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The registries of Athletes, Horses, Clubs and Officials are searched across Tenants by any signed-in account, each
/// through a named view of its own (ADR-0012, ADR-0010, #643): <c>/api/athletes/all-tenants</c> and the like. A search takes
/// <c>filter=contains(name,'text')</c>, whose words have to be parts of a name in any order and in any case, with a word of
/// at least two characters, the <c>sort</c> of the names and the pages of every list (ten rows a page, at most
/// twenty-five), answers with only the fields that are meant to leave, and shares the budget of searches with the search of
/// accounts, a page being a search. The rows of other Tenants are found only here: nothing that is sent to any other route
/// turns it across Tenants. The database matches, sorts and pages (the rest-api skill, ADR-0010).
/// </summary>
public sealed class RegistrySearchTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public RegistrySearchTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Theory]
    [InlineData("athletes", "athletes")]
    [InlineData("horses", "horses")]
    [InlineData("clubs", "clubs")]
    [InlineData("officials", "event_officials")]
    public async Task Any_signed_in_account_finds_the_rows_of_every_Tenant_through_the_named_view_of_the_registry(
        string registry,
        string collection
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var a = await TenantAsync(_mongo.ConnectionString);
        var b = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var inA = await AddAsync(collection, a, $"Alpha{marker} Name");
        var inB = await AddAsync(collection, b, $"Alpha{marker} Other");
        await AddAsync(collection, a, $"Beta{marker} Name");

        var response = await SearchAsync(person, registry, $"alpha{marker}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, x => Assert.Equal(registry, x.GetProperty("type").GetString()));
        Assert.Equal(
            new[] { inA.ToString(), inB.ToString() }.Order(),
            items.Select(x => x.GetProperty("id").GetString()!).Order()
        );
        Assert.Equal(
            new[] { a, b }.Order(),
            items.Select(x => x.GetProperty("attributes").GetProperty("tenantId").GetString()!).Order()
        );
        Assert.Equal(
            new[] { $"Alpha{marker} Name", $"Alpha{marker} Other" },
            items.Select(x => x.GetProperty("attributes").GetProperty("name").GetString()!)
        );
    }

    [Fact]
    public async Task An_Athlete_is_found_by_the_English_name_or_the_FEI_ID_and_shows_the_club_the_country_and_nothing_of_its_account()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        await AddAsync(
            "athletes",
            tenant,
            "Мария Попова",
            nameEnglish: $"Maria{marker} Popova",
            feiId: $"FEI{marker}",
            extra: new BsonDocument
            {
                {
                    "Club",
                    new BsonDocument { { "Name", "Rider Club" } }
                },
                {
                    "Country",
                    new BsonDocument { { "Name", "Bulgaria" } }
                },
                {
                    "User",
                    new BsonDocument
                    {
                        { "Email", "maria.popova@example.test" },
                        { "SecurityStamp", "stamp-value" },
                        {
                            "Roles",
                            new BsonArray { "official" }
                        },
                    }
                },
            }
        );

        var byEnglishName = await SearchAsync(person, "athletes", $"maria{marker}");
        var byFeiId = await SearchAsync(person, "athletes", $"fei{marker}");

        var text = await byEnglishName.Content.ReadAsStringAsync();
        var attributes = JsonDocument.Parse(text).RootElement.GetProperty("data")[0].GetProperty("attributes");
        Assert.Equal("Мария Попова", attributes.GetProperty("name").GetString());
        Assert.Equal($"Maria{marker} Popova", attributes.GetProperty("nameEnglish").GetString());
        Assert.Equal($"FEI{marker}", attributes.GetProperty("feiId").GetString());
        Assert.Equal("Rider Club", attributes.GetProperty("club").GetString());
        Assert.Equal("Bulgaria", attributes.GetProperty("country").GetString());
        Assert.DoesNotContain("maria.popova@example.test", text);
        Assert.DoesNotContain("stamp-value", text);
        Assert.DoesNotContain("official", text);
        Assert.Single((await ApiSessions.ReadJsonAsync(byFeiId)).GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task An_Official_is_found_once_with_the_role_whatever_the_number_of_Events_it_is_in()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        await AddAsync(
            "event_officials",
            await TenantAsync(_mongo.ConnectionString),
            $"Judge{marker} One",
            role: "GroundJury"
        );
        await AddAsync(
            "event_officials",
            await TenantAsync(_mongo.ConnectionString),
            $"Judge{marker} One",
            role: "GroundJury"
        );
        await AddAsync(
            "event_officials",
            await TenantAsync(_mongo.ConnectionString),
            $"Judge{marker} One",
            role: "Steward"
        );

        var response = await SearchAsync(person, "officials", $"judge{marker}");

        var roles = (await ApiSessions.ReadJsonAsync(response))
            .GetProperty("data")
            .EnumerateArray()
            .Select(x => x.GetProperty("attributes").GetProperty("role").GetString()!)
            .Order();
        Assert.Equal(new[] { "GroundJury", "Steward" }, roles);
    }

    [Theory]
    [InlineData("athletes")]
    [InlineData("horses")]
    [InlineData("clubs")]
    [InlineData("officials")]
    public async Task A_search_takes_only_the_filter_of_a_name_with_a_word_of_two_characters_and_no_parameter_widens_it(
        string registry
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var path = $"/api/{registry}/all-tenants";

        var tooShort = await SearchAsync(person, registry, "a");
        var noFilter = await person.Page.GetAsync(path);
        var otherMember = await person.Page.GetAsync($"{path}?filter=contains(tenantId,'abc')");
        var parameter = await person.Page.GetAsync($"{path}?filter=contains(name,'abc')&tenantId=other");

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal("search-too-short", await ErrorCodeAsync(tooShort));
        Assert.Equal("invalid-filter", await ErrorCodeAsync(noFilter));
        Assert.Equal("invalid-filter", await ErrorCodeAsync(otherMember));
        Assert.Equal("unsupported-parameter", await ErrorCodeAsync(parameter));
    }

    [Theory]
    [InlineData("athletes")]
    [InlineData("horses")]
    [InlineData("clubs")]
    [InlineData("officials")]
    public async Task Nobody_who_is_not_signed_in_searches_a_registry(string registry)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);

        var response = await client.GetAsync($"/api/{registry}/all-tenants?filter=contains(name,'abc')");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("not-signed-in", await ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData("athletes", "athletes")]
    [InlineData("horses", "horses")]
    [InlineData("clubs", "clubs")]
    [InlineData("officials", "event_officials")]
    public async Task A_search_is_answered_a_page_at_a_time_in_the_order_of_the_names_and_says_where_the_next_page_is(
        string registry,
        string collection
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 11; i >= 0; i--)
        {
            await AddAsync(collection, tenant, $"Many{marker} Row {i:00}"); // out of order, so that the order is the database's
        }

        var first = await ReadAsync(await SearchAsync(person, registry, $"many{marker}"));
        var second = await ReadAsync(await person.Page.GetAsync(first.Next!));

        Assert.Equal(Enumerable.Range(0, 10).Select(i => $"Many{marker} Row {i:00}"), first.Names);
        Assert.Equal((10, 1), (first.Size, first.Number));
        Assert.Equal([$"Many{marker} Row 10", $"Many{marker} Row 11"], second.Names);
        Assert.Equal((10, 2), (second.Size, second.Number));
        Assert.Null(second.Next);
        Assert.StartsWith($"/api/{registry}/all-tenants?filter=", first.Self, StringComparison.Ordinal); // where the list is, with what was asked for
        Assert.Equal(first.Next, second.Self); // and the next page is where the second is
    }

    [Fact]
    public async Task A_page_is_as_big_as_it_was_asked_for_up_to_twenty_five_rows_and_the_next_link_keeps_the_rest_of_the_query()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 27; i++)
        {
            await AddAsync("clubs", tenant, $"Many{marker} Club {i:00}");
        }

        var big = await ReadAsync(await SearchAsync(person, "clubs", $"many{marker}", Page(size: 25)));
        var rest = await ReadAsync(await person.Page.GetAsync(big.Next!));
        var small = await ReadAsync(await SearchAsync(person, "clubs", $"many{marker}", Page(size: 5, number: 6)));

        Assert.Equal(25, big.Names.Count);
        Assert.Equal((25, 1), (big.Size, big.Number));
        Assert.Equal([$"Many{marker} Club 25", $"Many{marker} Club 26"], rest.Names);
        Assert.Equal((25, 2), (rest.Size, rest.Number)); // the next page is as big as this one
        Assert.Null(rest.Next);
        Assert.Equal([$"Many{marker} Club 25", $"Many{marker} Club 26"], small.Names);
        Assert.Equal((5, 6), (small.Size, small.Number));
        Assert.Null(small.Next);
    }

    [Theory]
    [InlineData("athletes", "athletes")]
    [InlineData("horses", "horses")]
    [InlineData("clubs", "clubs")]
    public async Task Rows_with_the_same_name_are_never_twice_or_missing_between_two_pages(
        string registry,
        string collection
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var a = await TenantAsync(_mongo.ConnectionString);
        var b = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await AddAsync(collection, i % 2 == 0 ? a : b, $"Same{marker}"));
        }

        var pages = new List<Found> { await ReadAsync(await SearchAsync(person, registry, marker, Page(size: 2))) };
        for (var i = 0; i < 6 && pages[^1].Next != null; i++) // a next page that never ends is a failure and not a test that never ends
        {
            pages.Add(await ReadAsync(await person.Page.GetAsync(pages[^1].Next!)));
        }

        Assert.Equal([2, 2, 1], pages.Select(x => x.Names.Count));
        Assert.Equal(ids.Select(x => x.ToString()).Order(), pages.SelectMany(x => x.Ids).Order());
    }

    [Theory]
    [InlineData("athletes", "athletes")]
    [InlineData("horses", "horses")]
    [InlineData("clubs", "clubs")]
    [InlineData("officials", "event_officials")]
    public async Task The_names_are_in_order_and_sort_asks_for_the_other_way_round_and_the_next_page_keeps_it(
        string registry,
        string collection
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        foreach (var name in new[] { "Charlie", "Alpha", "Bravo" })
        {
            await AddAsync(collection, tenant, $"{name} {marker}");
        }

        var plain = await ReadAsync(await SearchAsync(person, registry, marker));
        var named = await ReadAsync(await SearchAsync(person, registry, marker, "&sort=name"));
        var backwards = await ReadAsync(await SearchAsync(person, registry, marker, "&sort=-name"));
        var firstOfTwo = await ReadAsync(await SearchAsync(person, registry, marker, "&sort=-name" + Page(size: 2)));
        var lastOfTwo = await ReadAsync(await person.Page.GetAsync(firstOfTwo.Next!));

        string[] ascending = [$"Alpha {marker}", $"Bravo {marker}", $"Charlie {marker}"];
        Assert.Equal(ascending, plain.Names);
        Assert.Equal(ascending, named.Names);
        Assert.Equal(ascending.Reverse(), backwards.Names);
        Assert.Equal([$"Charlie {marker}", $"Bravo {marker}"], firstOfTwo.Names);
        Assert.Equal([$"Alpha {marker}"], lastOfTwo.Names);
    }

    [Theory]
    [InlineData("&sort=id", "invalid-sort")]
    [InlineData("&sort=-id", "invalid-sort")]
    [InlineData("&sort=name,id", "invalid-sort")]
    [InlineData("&sort=", "invalid-sort")]
    [InlineData("&page%5Bsize%5D=26", "invalid-page")]
    [InlineData("&page%5Bsize%5D=0", "invalid-page")]
    [InlineData("&page%5Bsize%5D=ten", "invalid-page")]
    [InlineData("&page%5Bnumber%5D=0", "invalid-page")]
    [InlineData("&page%5Bnumber%5D=-1", "invalid-page")]
    [InlineData("&page%5Bnumber%5D=2147483647", "invalid-page")] // a page whose first row is beyond what a number can count
    public async Task A_sort_or_a_page_that_the_search_does_not_take_is_refused(string parameters, string code)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);

        foreach (var registry in new[] { "athletes", "horses", "clubs", "officials" })
        {
            var response = await SearchAsync(person, registry, "abc", parameters);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(code, await ErrorCodeAsync(response));
        }
    }

    [Fact]
    public async Task Every_word_of_the_text_is_part_of_one_of_the_names_in_any_order_in_any_case_and_in_either_script()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var maria = await AddAsync("athletes", tenant, $"Мария {cyrillic}ова", nameEnglish: $"Maria {latin}ova");
        await AddAsync("athletes", tenant, $"Мария Друга {cyrillic}", nameEnglish: $"Maria Other {latin}");

        async Task<IReadOnlyList<string>> FindAsync(string text)
        {
            return (await ReadAsync(await SearchAsync(person, "athletes", text))).Ids;
        }

        Assert.Equal([maria.ToString()], await FindAsync($"{latin.ToUpperInvariant()}OVA maria")); // the English name, the words the other way round, in capitals
        Assert.Equal([maria.ToString()], await FindAsync($"{cyrillic.ToUpperInvariant()}ОВА мария")); // the name, in capitals
        Assert.Equal([maria.ToString()], await FindAsync($"maria {cyrillic}ова")); // a word of each of its two names
        Assert.Equal([maria.ToString()], await FindAsync($"  {cyrillic[^3..]}ова   ")); // a part of a word, and the spaces around
        Assert.Empty(await FindAsync($"maria {latin}ova nobody")); // every word has to be there
        Assert.Empty(await FindAsync($"{latin}ova {cyrillic}ова зад")); // in the one name or the other
    }

    [Fact]
    public async Task What_is_asked_for_is_a_name_and_not_a_pattern()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        await AddAsync("clubs", tenant, $"Pattern{marker} Club");

        var everything = await ReadAsync(await SearchAsync(person, "clubs", ".*."));
        var brackets = await SearchAsync(person, "clubs", "[[[ (((");

        Assert.Empty(everything.Names);
        Assert.Equal(HttpStatusCode.OK, brackets.StatusCode);
    }

    [Theory]
    [InlineData("athletes")]
    [InlineData("horses")]
    [InlineData("clubs")]
    [InlineData("officials")]
    public async Task A_text_with_no_word_of_two_characters_is_too_short_however_long_it_is(string registry)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);

        foreach (var text in new[] { "a b", "a b c d e", "  a  " })
        {
            var tooShort = await SearchAsync(person, registry, text);

            Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
            Assert.Equal("search-too-short", await ErrorCodeAsync(tooShort));
        }

        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(person, registry, "a bc")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(person, registry, "ab")).StatusCode); // two characters are enough
    }

    [Fact]
    public async Task A_text_has_at_most_six_words_and_a_word_written_again_is_the_same_word()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);

        var six = await SearchAsync(person, "athletes", "one two three four five six");
        var seven = await SearchAsync(person, "athletes", "one two three four five six seven");
        var again = await SearchAsync(person, "athletes", "abc ABC Abc aBc abC ABc aBC abc"); // seven ways to write one word

        Assert.Equal(HttpStatusCode.OK, six.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, seven.StatusCode);
        Assert.Equal("invalid-filter", await ErrorCodeAsync(seven));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task A_text_with_a_null_character_is_refused_before_it_reaches_the_database()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);

        var response = await SearchAsync(person, "clubs", "ab\0cd");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-filter", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_last_page_that_is_exactly_full_has_no_next_page()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 10; i++)
        {
            await AddAsync("clubs", tenant, $"Ten{marker} Club {i}");
        }

        var whole = await ReadAsync(await SearchAsync(person, "clubs", $"ten{marker}"));
        var half = await ReadAsync(await SearchAsync(person, "clubs", $"ten{marker}", Page(size: 5)));
        var otherHalf = await ReadAsync(await person.Page.GetAsync(half.Next!));

        Assert.Equal(10, whole.Names.Count);
        Assert.Null(whole.Next); // the page is full, and nothing follows
        Assert.Equal(5, otherHalf.Names.Count);
        Assert.Null(otherHalf.Next);
    }

    [Theory]
    [InlineData("athletes", "athletes")]
    [InlineData("horses", "horses")]
    [InlineData("clubs", "clubs")]
    public async Task Rows_with_the_same_name_come_in_the_order_of_their_ids_whichever_way_the_names_are(
        string registry,
        string collection
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var ids = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            ids.Add(await AddAsync(collection, tenant, $"Tied{marker}"));
        }

        var ascending = await ReadAsync(await SearchAsync(person, registry, marker));
        var descending = await ReadAsync(await SearchAsync(person, registry, marker, "&sort=-name"));

        var inOrder = ids.Select(HexOf).Order(StringComparer.Ordinal).ToList(); // as MongoDB orders the bytes of an id
        Assert.Equal(inOrder, ascending.Ids.Select(x => HexOf(Guid.Parse(x))));
        Assert.Equal(inOrder, descending.Ids.Select(x => HexOf(Guid.Parse(x)))); // the names turn round, the ids do not
    }

    [Fact]
    public async Task An_Official_is_found_by_the_name_in_English_as_well()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var official = await AddAsync(
            "event_officials",
            tenant,
            $"Съдия {cyrillic}",
            nameEnglish: $"Judge {latin}",
            role: "Steward"
        );

        var found = await ReadAsync(await SearchAsync(person, "officials", $"judge {latin}"));

        Assert.Equal([official.ToString()], found.Ids);
    }

    [Fact]
    public async Task Officials_with_the_same_name_come_in_the_order_of_their_roles()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        foreach (var role in new[] { "Steward", "ChiefSteward", "GroundJury" })
        {
            await AddAsync("event_officials", tenant, $"Judge {marker}", role: role);
        }

        var response = await SearchAsync(person, "officials", marker);

        var roles = (await ApiSessions.ReadJsonAsync(response))
            .GetProperty("data")
            .EnumerateArray()
            .Select(x => x.GetProperty("attributes").GetProperty("role").GetString()!);
        Assert.Equal(["ChiefSteward", "GroundJury", "Steward"], roles);
    }

    [Fact]
    public async Task Each_page_is_a_search_that_draws_on_the_budget_of_the_account()
    {
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            configureHost: builder => builder.UseSetting("Search:RateLimits:PerAccount", "2")
        );
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 3; i++)
        {
            await AddAsync("clubs", tenant, $"Few{marker} Club {i}");
        }

        var first = await ReadAsync(await SearchAsync(person, "clubs", $"few{marker}", Page(size: 1)));
        var second = await ReadAsync(await person.Page.GetAsync(first.Next!));
        var third = await person.Page.GetAsync(second.Next!);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("rate-limited", await ErrorCodeAsync(third));
    }

    [Fact]
    public async Task At_most_ten_rows_are_answered_and_the_searches_of_every_registry_and_of_accounts_share_one_budget()
    {
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            configureHost: builder => builder.UseSetting("Search:RateLimits:PerAccount", "4")
        );
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 12; i++)
        {
            await AddAsync("clubs", tenant, $"Many{marker} Club {i:00}");
        }

        var first = await SearchAsync(person, "clubs", $"many{marker}");
        var second = await SearchAsync(person, "athletes", "abc");
        var third = await SearchAsync(person, "horses", "abc");
        var fourth = await person.Page.GetAsync("/api/accounts?filter=contains(name,'abc')");
        var fifth = await SearchAsync(person, "officials", "abc");

        Assert.Equal(10, (await ApiSessions.ReadJsonAsync(first)).GetProperty("data").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fourth.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
        Assert.Equal("rate-limited", await ErrorCodeAsync(fifth));
    }

    static Task<HttpResponseMessage> SearchAsync(Person person, string registry, string text, string parameters = "")
    {
        var filter = Uri.EscapeDataString($"contains(name,'{text.Replace("'", "''")}')");
        return person.Page.GetAsync($"/api/{registry}/all-tenants?filter={filter}{parameters}");
    }

    /// <summary>The bytes of an id in the order MongoDB compares them, as text.</summary>
    static string HexOf(Guid id)
    {
        return Convert.ToHexString(id.ToByteArray(bigEndian: true));
    }

    /// <summary>The parameters of a page, written as a client writes them (the brackets are escaped).</summary>
    static string Page(int? size = null, int? number = null)
    {
        return (size == null ? string.Empty : $"&page%5Bsize%5D={size}")
            + (number == null ? string.Empty : $"&page%5Bnumber%5D={number}");
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
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
            [.. rows.Select(x => x.GetProperty("attributes").GetProperty("name").GetString()!)],
            [.. rows.Select(x => x.GetProperty("id").GetString()!)],
            page.GetProperty("size").GetInt32(),
            page.GetProperty("number").GetInt32(),
            links.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null,
            links.GetProperty("self").GetString()!
        );
    }

    async Task<Guid> AddAsync(
        string collection,
        string tenant,
        string name,
        string? nameEnglish = null,
        string? feiId = null,
        string? role = null,
        BsonDocument? extra = null
    )
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", new BsonBinaryData(id, GuidRepresentation.Standard) },
            { "TenantId", tenant },
            { "Name", name },
        };
        if (nameEnglish != null)
        {
            document["NameEnglish"] = nameEnglish;
        }

        if (feiId != null)
        {
            document["FeiId"] = feiId;
        }

        if (role != null)
        {
            document["Role"] = role;
        }

        if (extra != null)
        {
            document.AddRange(extra);
        }

        await new MongoClient(_mongo.ConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>(collection)
            .InsertOneAsync(document);
        return id;
    }

    sealed class Found
    {
        public Found(List<string> names, List<string> ids, int size, int number, string? next, string self)
        {
            Names = names;
            Ids = ids;
            Size = size;
            Number = number;
            Next = next;
            Self = self;
        }

        public List<string> Names { get; }
        public List<string> Ids { get; }
        public int Size { get; }
        public int Number { get; }
        public string? Next { get; }
        public string Self { get; }
    }
}
