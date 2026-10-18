using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The search for a Participant, an Athlete or a Horse, by name (#638, ADR-0010, the rest-api skill): <c>/api/participants</c>,
/// over the registries of every Tenant, for any signed-in account. A Participant is found in either script by a few letters of
/// each word of the name in any order; a match tells its kind, its id and the name in the language of the request, and nothing
/// else of the row.
/// </summary>
public sealed class ParticipantsTests : IClassFixture<MongoFixture>
{
    // The letters of the marker are the ones that a Latin name and a Cyrillic one write the same, so that a marker that is in
    // one script is found by the other. The database is shared by every test, and a search reads all of it.
    const string LATIN = "bdlmnprt";
    const string CYRILLIC = "бдлмнпрт";

    readonly MongoFixture _mongo;

    public ParticipantsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Latin_text_finds_a_Cyrillic_name_and_the_other_way_round_across_Tenants_and_a_match_tells_its_kind()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var (latin, cyrillic) = Marker();
        var athlete = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            await TenantAsync(_mongo.ConnectionString),
            $"Иван {cyrillic}ов",
            RegistrySeed.CountryOf("Bulgaria", "BG")
        );
        var horse = await RegistrySeed.HorseAsync(
            _mongo.ConnectionString,
            await TenantAsync(_mongo.ConnectionString),
            $"Star {latin}"
        );
        await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            await TenantAsync(_mongo.ConnectionString),
            "Георги Другов",
            RegistrySeed.CountryOf("Bulgaria", "BG")
        );
        var englishOnly = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            await TenantAsync(_mongo.ConnectionString),
            "Нов Атлет", // nothing of the text is in this name, but it is in the name it was registered with in English
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: $"Rider {latin}"
        );

        var byLatin = await MatchesAsync(person, $"{latin}ov iv");
        var byCyrillic = await MatchesAsync(person, cyrillic);
        var byPartsInAnyOrder = await MatchesAsync(person, $"star {latin}");
        var byTheEnglishName = await MatchesAsync(person, $"rider {latin}");

        var athleteMatch = Assert.Single(byLatin);
        Assert.Equal("participants", athleteMatch.GetProperty("type").GetString());
        Assert.Equal(athlete.ToString(), athleteMatch.GetProperty("id").GetString());
        Assert.Equal("Athlete", athleteMatch.GetProperty("attributes").GetProperty("kind").GetString());
        Assert.Equal(
            new[] { (athlete.ToString(), "Athlete"), (englishOnly.ToString(), "Athlete"), (horse.ToString(), "Horse") }, // by name, as it reads in Latin letters: ivan, nov, star
            byCyrillic.Select(x =>
                (x.GetProperty("id").GetString()!, x.GetProperty("attributes").GetProperty("kind").GetString()!)
            )
        );
        Assert.Equal(englishOnly.ToString(), Assert.Single(byTheEnglishName).GetProperty("id").GetString());
        var horseMatch = Assert.Single(byPartsInAnyOrder);
        Assert.Equal(horse.ToString(), horseMatch.GetProperty("id").GetString());
        Assert.Equal("Horse", horseMatch.GetProperty("attributes").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task A_match_tells_the_kind_the_id_and_the_name_in_the_language_of_the_request_and_nothing_else_of_the_row()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var (latin, cyrillic) = Marker();
        var tenant = await TenantAsync(_mongo.ConnectionString);
        await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            $"Иван {cyrillic}ов",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: $"Ivan {latin}ov",
            feiId: $"FEI{latin}",
            club: new BsonDocument { { "Name", "Rider Club" } },
            user: new BsonDocument { { "Email", "ivan.leaks@example.test" }, { "SecurityStamp", "stamp-value" } }
        );
        var path = $"/api/participants?filter={Uri.EscapeDataString($"contains(name,'{latin}ov')")}";

        var inEnglish = await person.Page.GetAsync(path);
        var withoutLanguage = await person.Page.GetAsync(path);
        using var inBulgarian = new HttpRequestMessage(HttpMethod.Get, path);
        inBulgarian.Headers.AcceptLanguage.ParseAdd("bg");
        var bulgarian = await person.Page.SendAsync(inBulgarian);

        var text = await inEnglish.Content.ReadAsStringAsync();
        var english = JsonDocument.Parse(text).RootElement.GetProperty("data")[0].GetProperty("attributes");
        Assert.Equal(["kind", "displayName"], english.EnumerateObject().Select(x => x.Name));
        Assert.Equal($"Ivan {latin}ov", english.GetProperty("displayName").GetString());
        Assert.Equal(
            $"Ivan {latin}ov",
            (await ApiSessions.ReadJsonAsync(withoutLanguage))
                .GetProperty("data")[0]
                .GetProperty("attributes")
                .GetProperty("displayName")
                .GetString()
        );
        Assert.Equal(
            $"Иван {cyrillic}ов",
            (await ApiSessions.ReadJsonAsync(bulgarian))
                .GetProperty("data")[0]
                .GetProperty("attributes")
                .GetProperty("displayName")
                .GetString()
        );
        Assert.Contains("Accept-Language", inEnglish.Headers.Vary);
        Assert.DoesNotContain(tenant, text);
        Assert.DoesNotContain($"FEI{latin}", text);
        Assert.DoesNotContain("Rider Club", text);
        Assert.DoesNotContain("Bulgaria", text);
        Assert.DoesNotContain("ivan.leaks@example.test", text);
        Assert.DoesNotContain("stamp-value", text);
    }

    [Fact]
    public async Task A_search_takes_only_the_filter_of_a_name_of_at_least_three_characters_and_no_parameter_widens_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        const string path = "/api/participants";

        var tooShort = await person.Page.GetAsync($"{path}?filter=contains(name,'ab')");
        var noFilter = await person.Page.GetAsync(path);
        var otherMember = await person.Page.GetAsync($"{path}?filter=contains(tenantId,'abc')");
        var parameter = await person.Page.GetAsync($"{path}?filter=contains(name,'abc')&tenantId=other");

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal("search-too-short", await ErrorCodeAsync(tooShort));
        Assert.Equal("invalid-filter", await ErrorCodeAsync(noFilter));
        Assert.Equal("invalid-filter", await ErrorCodeAsync(otherMember));
        Assert.Equal("unsupported-parameter", await ErrorCodeAsync(parameter));
    }

    [Fact]
    public async Task Nobody_who_is_not_signed_in_searches_the_registries_by_name()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);

        var response = await client.GetAsync("/api/participants?filter=contains(name,'abc')");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("not-signed-in", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task At_most_ten_matches_are_answered_and_the_search_shares_the_budget_of_the_searches_of_the_registries()
    {
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            configureHost: builder => builder.UseSetting("Search:RateLimits:PerAccount", "3")
        );
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var (latin, _) = Marker();
        for (var i = 0; i < 12; i++)
        {
            await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, $"Many {latin} {i:00}");
        }

        var first = await person.Page.GetAsync(Path(latin));
        var second = await person.Page.GetAsync("/api/horses/all-tenants?filter=contains(name,'abc')");
        var third = await person.Page.GetAsync(Path("abc"));
        var fourth = await person.Page.GetAsync(Path("abc"));

        Assert.Equal(10, (await ApiSessions.ReadJsonAsync(first)).GetProperty("data").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.Equal("rate-limited", await ErrorCodeAsync(fourth));

        static string Path(string text)
        {
            return $"/api/participants?filter={Uri.EscapeDataString($"contains(name,'{text}')")}";
        }
    }

    static (string Latin, string Cyrillic) Marker()
    {
        var letters = Enumerable.Range(0, 8).Select(_ => Random.Shared.Next(LATIN.Length)).ToList();
        return (string.Concat(letters.Select(x => LATIN[x])), string.Concat(letters.Select(x => CYRILLIC[x])));
    }

    static async Task<List<JsonElement>> MatchesAsync(Person person, string text)
    {
        var response = await person.Page.GetAsync(
            $"/api/participants?filter={Uri.EscapeDataString($"contains(name,'{text}')")}"
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").EnumerateArray()];
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
