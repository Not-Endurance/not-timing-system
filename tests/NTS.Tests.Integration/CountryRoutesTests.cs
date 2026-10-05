using System.Net;
using System.Text.Json;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The countries (#603): the platform's reference data, which belongs to no Tenant. Every signed-in account reads them,
/// and the Developer alone keeps them (ADR-0012: the platform owner). They are served the way every family of reference
/// data is (see <c>ClubRoutesTests</c>), except that a country is not removed: Events and Athletes embed one.
/// </summary>
public sealed class CountryRoutesTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public CountryRoutesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Any_signed_in_account_reads_the_countries_whatever_its_Tenant()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var stateless = await SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var iso = CountrySeed.UniqueIsoCode();
        var id = await CountrySeed.AddAsync(
            _mongo.ConnectionString,
            "Land " + iso,
            iso,
            nfCode: iso[..3],
            locale: "xx-XX"
        );

        var byMember = await member.Page.GetAsync($"/api/countries/{id}");
        var byStateless = await ListAsync(stateless, $"?filter=isoCode eq '{iso}'");

        Assert.Equal(HttpStatusCode.OK, byMember.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(byMember)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("Land " + iso, attributes.GetProperty("name").GetString());
        Assert.Equal(iso, attributes.GetProperty("isoCode").GetString());
        Assert.Equal(iso[..3], attributes.GetProperty("nfCode").GetString());
        Assert.Equal("xx-XX", attributes.GetProperty("locale").GetString());
        Assert.Equal(
            id.ToString(),
            Assert.Single(byStateless.GetProperty("data").EnumerateArray()).GetProperty("id").GetString()
        );
    }

    [Fact]
    public async Task A_caller_who_is_not_signed_in_reads_no_country()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);

        var response = await client.GetAsync("/api/countries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_Developer_makes_a_country_with_the_id_it_names_and_it_belongs_to_no_Tenant()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, home: null, isDeveloper: true);
        var iso = CountrySeed.UniqueIsoCode();
        var id = Guid.NewGuid();

        var response = await developer.Page.WriteAsync(
            HttpMethod.Post,
            "/api/countries",
            "countries",
            new
            {
                name = "Land " + iso,
                isoCode = iso,
                nfCode = iso[..3],
                locale = "xx-XX",
            },
            id: id.ToString()
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/countries/{id}", response.Headers.Location?.ToString());
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "countries", id))!;
        Assert.Equal("nts", stored["TenantId"].AsString);
        Assert.Equal(iso, stored["IsoCode"].AsString);
        Assert.Equal("xx-XX", stored["Locale"].AsString);
    }

    [Fact]
    public async Task The_countries_are_kept_by_the_Developer_alone()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var iso = CountrySeed.UniqueIsoCode();
        var existing = await CountrySeed.AddAsync(_mongo.ConnectionString, "Land " + iso, iso);

        foreach (var person in new[] { root, mainOperator })
        {
            var made = await person.Page.WriteAsync(
                HttpMethod.Post,
                "/api/countries",
                "countries",
                new { name = "Mine" }
            );
            var changed = await person.Page.WriteAsync(
                HttpMethod.Patch,
                $"/api/countries/{existing}",
                "countries",
                new { name = "Taken" }
            );

            Assert.Equal(HttpStatusCode.Forbidden, made.StatusCode);
            Assert.Equal("not-developer", await ErrorCodeAsync(made));
            Assert.Equal(HttpStatusCode.Forbidden, changed.StatusCode);
        }

        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "countries", existing))!;
        Assert.Equal("Land " + iso, stored["Name"].AsString);
    }

    [Fact]
    public async Task The_Developer_changes_the_members_it_names_and_a_member_sent_as_null_is_taken_away()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, home: null, isDeveloper: true);
        var iso = CountrySeed.UniqueIsoCode();
        var id = await CountrySeed.AddAsync(
            _mongo.ConnectionString,
            "Land " + iso,
            iso,
            nfCode: iso[..3],
            locale: "xx-XX"
        );

        var renamed = await developer.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/countries/{id}",
            "countries",
            new { name = "Renamed " + iso }
        );
        var cleared = await developer.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/countries/{id}",
            "countries",
            new { locale = (string?)null }
        );

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "countries", id))!;
        Assert.Equal("Renamed " + iso, stored["Name"].AsString);
        Assert.Equal(iso[..3], stored["NfCode"].AsString);
        Assert.False(stored.Contains("Locale"));
        Assert.Equal("nts", stored["TenantId"].AsString);
    }

    [Fact]
    public async Task A_country_needs_a_name_and_there_is_no_route_that_removes_one()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, home: null, isDeveloper: true);
        var iso = CountrySeed.UniqueIsoCode();
        var id = await CountrySeed.AddAsync(_mongo.ConnectionString, "Land " + iso, iso);

        var nameless = await developer.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/countries/{id}",
            "countries",
            new { name = (string?)null }
        );
        var removed = await developer.Page.DeleteAsync($"/api/countries/{id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, nameless.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        Assert.Equal(
            "Land " + iso,
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "countries", id))!["Name"].AsString
        );
    }

    static async Task<JsonElement> ListAsync(Person person, string query)
    {
        var response = await person.Page.GetAsync("/api/countries" + query);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{query} answered {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"
        );
        return await ApiSessions.ReadJsonAsync(response);
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
