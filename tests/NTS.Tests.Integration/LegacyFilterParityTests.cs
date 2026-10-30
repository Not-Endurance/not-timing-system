using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Functions API read the lists of the Clubs and the Athletes with an OData <c>$filter</c> over the members of the model
/// it served, spelled as the model spells them (<c>Name</c>), and the Api reads them with <c>filter</c> over the same
/// members in camelCase (<c>name</c>), #603. The two are given the same stored rows and the same expression, and what the
/// one finds the other finds: the Ui's filters mean what they meant when they move to the Api. The lists of the Horses, the
/// countries and the Setups took no filter in the Functions API (it listed every row whatever the query said), so for them
/// the same rows are listed, and the filter the Api takes on top of that is the one of the route tests. Needs the
/// Functions API and Azurite, like the coexistence tests.
/// </summary>
public sealed class LegacyFilterParityTests : IClassFixture<NtsIntegrationFixture>, IDisposable
{
    readonly NtsIntegrationFixture _fixture;
    readonly HttpClient _legacy;

    public LegacyFilterParityTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
        _legacy = new HttpClient { BaseAddress = fixture.FunctionsBaseUrl };
    }

    [Fact]
    public async Task The_clubs_an_expression_finds_are_the_ones_the_Functions_API_found_for_it()
    {
        await using var api = new ApiFactory(_fixture.MongoConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_fixture.MongoConnectionString);
        var member = await SignedInAsync(api, client, _fixture.MongoConnectionString, tenant);
        var alpha = await RegistrySeed.ClubAsync(_fixture.MongoConnectionString, tenant, "Alpha Riders");
        await RegistrySeed.ClubAsync(_fixture.MongoConnectionString, tenant, "Beta Riders");
        await RegistrySeed.ClubAsync(_fixture.MongoConnectionString, tenant, "Gamma Club");
        await RegistrySeed.ClubAsync(_fixture.MongoConnectionString, tenant, "O'Brien Club");

        await AssertSameAsync(
            member,
            "clubs",
            "clubs",
            ("Name eq 'Beta Riders'", ["Beta Riders"]),
            ("Name ne 'Beta Riders'", ["Alpha Riders", "Gamma Club", "O'Brien Club"]),
            ("contains(Name,'Riders')", ["Alpha Riders", "Beta Riders"]),
            ("startswith(Name,'Gamma')", ["Gamma Club"]),
            ("endswith(Name,'Club')", ["Gamma Club", "O'Brien Club"]),
            ("Name eq 'Alpha Riders' or Name eq 'Gamma Club'", ["Alpha Riders", "Gamma Club"]),
            ("contains(Name,'Riders') and Name ne 'Alpha Riders'", ["Beta Riders"]),
            ("Name eq 'O''Brien Club'", ["O'Brien Club"]),
            ($"Id eq {alpha}", ["Alpha Riders"]),
            ("tolower(Name) eq 'gamma club'", ["Gamma Club"]),
            ("Name eq 'Nobody'", [])
        );
    }

    [Fact]
    public async Task The_Athletes_an_expression_finds_are_the_ones_the_Functions_API_found_for_it()
    {
        await using var api = new ApiFactory(_fixture.MongoConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_fixture.MongoConnectionString);
        var member = await SignedInAsync(api, client, _fixture.MongoConnectionString, tenant);
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        var turkey = RegistrySeed.CountryOf("Turkey", "TR");
        var sofia = new BsonDocument
        {
            { "_id", RegistrySeed.Binary(Guid.NewGuid()) },
            { "TenantId", tenant },
            { "Name", "Sofia Riders" },
        };
        await RegistrySeed.AthleteAsync(
            _fixture.MongoConnectionString,
            tenant,
            "Ivan",
            bulgaria,
            club: sofia,
            feiId: "10000001"
        );
        await RegistrySeed.AthleteAsync(_fixture.MongoConnectionString, tenant, "Mehmet", turkey, feiId: "10000002");
        await RegistrySeed.AthleteAsync(_fixture.MongoConnectionString, tenant, "Anna", bulgaria);

        await AssertSameAsync(
            member,
            "athletes",
            "athletes",
            ("Name eq 'Ivan'", ["Ivan"]),
            ("Name ne 'Ivan'", ["Anna", "Mehmet"]),
            ("startswith(Name,'A') or FeiId eq '10000002'", ["Anna", "Mehmet"]),
            ("FeiId eq '10000002'", ["Mehmet"]),
            ("FeiId eq null", ["Anna"]),
            ("FeiId ne null", ["Ivan", "Mehmet"]),
            ("Name eq 'Nobody'", [])
        );
    }

    [Fact]
    public async Task The_lists_of_the_Horses_the_countries_and_the_Setups_are_the_rows_the_Functions_API_listed()
    {
        await using var api = new ApiFactory(_fixture.MongoConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_fixture.MongoConnectionString);
        var root = await SignedInAsync(api, client, _fixture.MongoConnectionString, tenant, TenantRootOf(tenant));
        await RegistrySeed.HorseAsync(_fixture.MongoConnectionString, tenant, "Burya", "Storm", "10000001");
        await RegistrySeed.HorseAsync(_fixture.MongoConnectionString, tenant, "Ruzgar", "Wind", "10000002");
        await RegistrySeed.HorseAsync(_fixture.MongoConnectionString, tenant, "Plain");
        await CountrySeed.AddAsync(_fixture.MongoConnectionString, "Parityland Alpha", "PQA", nfCode: "PAA");
        await CountrySeed.AddAsync(_fixture.MongoConnectionString, "Parityland Beta", "PQB");
        await CountrySeed.AddAsync(_fixture.MongoConnectionString, "Elsewhere", "PQE");
        await EventSeed.SetupAsync(_fixture.MongoConnectionString, tenant, root.Id, "Spring Ride");
        await EventSeed.SetupAsync(_fixture.MongoConnectionString, tenant, root.Id, "Summer Ride");

        await AssertSameAsync(root, "horses", "horses", (null, ["Burya", "Plain", "Ruzgar"]));
        await AssertSameAsync(
            root,
            "countries",
            "countries",
            (null, ["Elsewhere", "Parityland Alpha", "Parityland Beta"])
        );
        await AssertSameAsync(root, "configure-event", "configure-events", (null, ["Spring Ride", "Summer Ride"]));
    }

    [Theory]
    [InlineData("Name eq")]
    [InlineData("Name eq 'x' and")]
    [InlineData("Nope eq 'x'")]
    [InlineData("contains(Name)")]
    public async Task An_expression_the_Functions_API_refused_is_refused_by_the_Api_too(string expression)
    {
        await using var api = new ApiFactory(_fixture.MongoConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_fixture.MongoConnectionString);
        var member = await SignedInAsync(api, client, _fixture.MongoConnectionString, tenant);

        var legacy = await _legacy.GetAsync($"api/clubs?$filter={Uri.EscapeDataString(expression)}");
        var current = await member.Page.GetAsync("/api/clubs?filter=" + Uri.EscapeDataString(CamelCased(expression)));

        Assert.Equal(HttpStatusCode.BadRequest, legacy.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, current.StatusCode);
    }

    public void Dispose()
    {
        _legacy.Dispose();
    }

    /// <summary>The expression of the Functions API in the spelling of the Api: the members of a model are camelCase.</summary>
    static string CamelCased(string expression)
    {
        var result = new System.Text.StringBuilder(expression.Length);
        var quoted = false;
        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (c == '\'')
            {
                quoted = !quoted;
            }

            var startsAName = !quoted && char.IsUpper(c) && (i == 0 || !char.IsLetterOrDigit(expression[i - 1]));
            result.Append(startsAName ? char.ToLowerInvariant(c) : c);
        }

        return result.ToString();
    }

    static JsonElement Member(JsonElement element, string name)
    {
        return element
            .EnumerateObject()
            .First(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    static string Describe(IEnumerable<(Guid Id, string Name)> found)
    {
        return "[" + string.Join(", ", found.Select(x => x.Name)) + "]";
    }

    async Task AssertSameAsync(
        Person who,
        string legacyRoute,
        string route,
        params (string? Expression, string[] Names)[] cases
    )
    {
        foreach (var (expression, names) in cases)
        {
            var legacy = await ReadLegacyAsync(legacyRoute, expression);
            var current = await ReadCurrentAsync(who, route, expression);

            Assert.True(
                legacy.SequenceEqual(current),
                $"For '{expression}' in {route} the Functions API found {Describe(legacy)} and the Api {Describe(current)}."
            );
            Assert.True(
                names.Order(StringComparer.Ordinal).SequenceEqual(legacy.Select(x => x.Name), StringComparer.Ordinal),
                $"Both found {Describe(legacy)} for '{expression}' in {route}, not [{string.Join(", ", names)}]."
            );
        }
    }

    async Task<List<(Guid Id, string Name)>> ReadLegacyAsync(string route, string? expression)
    {
        var query = expression == null ? "" : $"?$filter={Uri.EscapeDataString(expression)}";
        using var response = await _legacy.GetAsync($"api/{route}{query}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"The Functions API answered {response.StatusCode} to {route} with '{expression}': {body}"
        );
        using var document = JsonDocument.Parse(body);
        var found = Member(document.RootElement, "Data")
            .EnumerateArray()
            .Select(x => (Guid.Parse(Member(x, "Id").GetString()!), Member(x, "Name").GetString()!));
        return Ordered(found);
    }

    async Task<List<(Guid Id, string Name)>> ReadCurrentAsync(Person who, string route, string? expression)
    {
        var spelled = expression == null ? null : CamelCased(expression);
        var query = spelled == null ? "" : $"?filter={Uri.EscapeDataString(spelled)}";
        var response = await who.Page.GetAsync($"/api/{route}{query}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"The Api answered {response.StatusCode} to {route} with '{spelled}': {body}"
        );
        using var document = JsonDocument.Parse(body);
        var found = document
            .RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(x =>
                (
                    Guid.Parse(x.GetProperty("id").GetString()!),
                    x.GetProperty("attributes").GetProperty("name").GetString()!
                )
            );
        return Ordered(found);
    }

    static List<(Guid Id, string Name)> Ordered(IEnumerable<(Guid Id, string Name)> found)
    {
        return found.OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Id).ToList();
    }
}
