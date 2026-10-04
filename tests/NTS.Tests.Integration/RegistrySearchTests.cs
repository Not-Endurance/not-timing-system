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
/// <c>filter=contains(name,'text')</c> and nothing else, at least three characters, answers at most ten rows with only the
/// fields that are meant to leave, and shares the budget of searches with the search of accounts. The rows of other
/// Tenants are found only here: nothing that is sent to any other route turns it across Tenants.
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
    public async Task A_search_takes_only_the_filter_of_a_name_of_at_least_three_characters_and_no_parameter_widens_it(
        string registry
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var path = $"/api/{registry}/all-tenants";

        var tooShort = await SearchAsync(person, registry, "ab");
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

    static Task<HttpResponseMessage> SearchAsync(Person person, string registry, string text)
    {
        var filter = Uri.EscapeDataString($"contains(name,'{text.Replace("'", "''")}')");
        return person.Page.GetAsync($"/api/{registry}/all-tenants?filter={filter}");
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
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
}
