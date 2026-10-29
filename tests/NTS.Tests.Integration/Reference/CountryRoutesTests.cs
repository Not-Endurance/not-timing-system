using System.Net;
using System.Text.Json;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The countries (#603): the platform's reference data, which belongs to no Tenant. Every signed-in account reads them,
/// and the Developer alone keeps them (ADR-0012: the platform owner). They are served the way every family of reference
/// data is (see <c>ClubRoutesTests</c>), except that a country is not removed: Events and Athletes embed one.
/// </summary>
public sealed class CountryRoutesTests : IClassFixture<SharedApiFixture>
{
    readonly SharedApiFixture _shared;

    public CountryRoutesTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task Any_signed_in_account_reads_the_countries_whatever_its_Tenant()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var member = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var stateless = await SignedInAsync(api, client, _shared.MongoConnectionString, home: null);
        var iso = CountrySeed.UniqueIsoCode();
        var id = await CountrySeed.AddAsync(
            _shared.MongoConnectionString,
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
        var api = _shared.Api;
        using var client = ApiClients.Of(api);

        var response = await client.GetAsync("/api/countries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_Developer_makes_a_country_with_the_id_it_names_and_it_belongs_to_no_Tenant()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var developer = await SignedInAsync(api, client, _shared.MongoConnectionString, home: null, isDeveloper: true);
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
        var stored = (await RegistrySeed.StoredAsync(_shared.MongoConnectionString, "countries", id))!;
        Assert.Equal("nts", stored["TenantId"].AsString);
        Assert.Equal(iso, stored["IsoCode"].AsString);
        Assert.Equal("xx-XX", stored["Locale"].AsString);
    }

    [Fact]
    public async Task The_countries_are_kept_by_the_Developer_alone()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        await EventSeed.SetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id);
        var iso = CountrySeed.UniqueIsoCode();
        var existing = await CountrySeed.AddAsync(_shared.MongoConnectionString, "Land " + iso, iso);

        foreach (var person in new[] { root, consoleOperator })
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

        var stored = (await RegistrySeed.StoredAsync(_shared.MongoConnectionString, "countries", existing))!;
        Assert.Equal("Land " + iso, stored["Name"].AsString);
    }

    [Fact]
    public async Task The_Developer_changes_the_members_it_names_and_a_member_sent_as_null_is_taken_away()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var developer = await SignedInAsync(api, client, _shared.MongoConnectionString, home: null, isDeveloper: true);
        var iso = CountrySeed.UniqueIsoCode();
        var id = await CountrySeed.AddAsync(
            _shared.MongoConnectionString,
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
        var stored = (await RegistrySeed.StoredAsync(_shared.MongoConnectionString, "countries", id))!;
        Assert.Equal("Renamed " + iso, stored["Name"].AsString);
        Assert.Equal(iso[..3], stored["NfCode"].AsString);
        Assert.False(stored.Contains("Locale"));
        Assert.Equal("nts", stored["TenantId"].AsString);
    }

    [Fact]
    public async Task A_country_needs_a_name_and_there_is_no_route_that_removes_one()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var developer = await SignedInAsync(api, client, _shared.MongoConnectionString, home: null, isDeveloper: true);
        var iso = CountrySeed.UniqueIsoCode();
        var id = await CountrySeed.AddAsync(_shared.MongoConnectionString, "Land " + iso, iso);

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
            (await RegistrySeed.StoredAsync(_shared.MongoConnectionString, "countries", id))!["Name"].AsString
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
