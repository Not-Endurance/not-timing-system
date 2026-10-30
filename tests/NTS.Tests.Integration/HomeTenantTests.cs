using System.Net;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// A person who had an account before Tenants (#601, ADR-0012) gets a home Tenant from the country of their profile
/// the next time they sign in, with the matching the profile screen uses (name, ISO code or NF code, in any case). A
/// profile that names no country, or one that no country with an ISO code matches, leaves them without a Tenant until
/// they pick a country. A home Tenant is set once and profile edits never move it. Every country here has a name of
/// its own: the countries are shared by the tests of the class, and the first match would win.
/// </summary>
public sealed class HomeTenantTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public HomeTenantTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Theory]
    [InlineData("name")]
    [InlineData("name in capitals")]
    [InlineData("iso code")]
    [InlineData("iso code in capitals")]
    [InlineData("nf code")]
    [InlineData("nf code in lower case")]
    public async Task A_person_from_before_Tenants_is_placed_in_the_Tenant_of_the_country_their_profile_names(
        string how
    )
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var country = await NewCountryAsync();
        var profile = how switch
        {
            "name" => country.Name,
            "name in capitals" => country.Name.ToUpperInvariant(),
            "iso code" => country.IsoCode.ToLowerInvariant(),
            "iso code in capitals" => country.IsoCode,
            "nf code" => country.NfCode,
            _ => country.NfCode.ToLowerInvariant(),
        };
        var email = UserSeed.NewEmail("legacy");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email, shape: x => x["CountryRegion"] = profile);

        await ApiSessions.SignInAsync(api, client, email);

        var user = (await ApiClients.FindUserAsync(_mongo.ConnectionString, email))!;
        var tenantId = $"country-{country.IsoCode.ToLowerInvariant()}";
        Assert.Equal(tenantId, user["HomeTenantId"].AsString);
        var membership = Assert.Single(user["Memberships"].AsBsonArray).AsBsonDocument;
        Assert.Equal(tenantId, membership["TenantId"].AsString);
        Assert.Empty(membership["Roles"].AsBsonArray);
        var tenant = (await ApiClients.FindTenantAsync(_mongo.ConnectionString, tenantId))!;
        Assert.Equal(country.Name, tenant["Name"].AsString);
        Assert.Equal("country", tenant["Kind"].AsString);
        Assert.Equal(profile, user["CountryRegion"].AsString); // the profile is not touched
        Assert.Equal("nts", user["TenantId"].AsString);
        Assert.Equal("official", Assert.Single(user["Roles"].AsBsonArray).AsString);
    }

    [Fact]
    public async Task People_of_one_country_share_the_Tenant_it_has_and_signing_in_again_changes_nothing()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new ApiFactory(_mongo.ConnectionString, time: time);
        using var client = ApiClients.Of(api);
        var country = await NewCountryAsync();
        var first = UserSeed.NewEmail("first");
        var second = UserSeed.NewEmail("second");
        await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            first,
            shape: x => x["CountryRegion"] = country.Name
        );
        await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            second,
            shape: x => x["CountryRegion"] = country.Name
        );

        await ApiSessions.SignInAsync(api, client, first);
        await ApiSessions.SignInAsync(api, client, second);
        var before = (await ApiClients.FindUserAsync(_mongo.ConnectionString, first))!;
        time.Advance(TimeSpan.FromMinutes(2));
        await ApiSessions.SignInAsync(api, client, first);

        var tenantId = $"country-{country.IsoCode.ToLowerInvariant()}";
        Assert.Equal(1, await ApiClients.CountTenantsAsync(_mongo.ConnectionString, tenantId));
        var after = (await ApiClients.FindUserAsync(_mongo.ConnectionString, first))!;
        Assert.Equal(before["HomeTenantId"], after["HomeTenantId"]);
        Assert.Equal(before["Memberships"], after["Memberships"]);
    }

    [Theory]
    [InlineData("Atlantis")]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_profile_that_names_no_country_leaves_the_person_without_a_Tenant_and_signing_in_still_works(
        string? profile
    )
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        await NewCountryAsync();
        var email = UserSeed.NewEmail("unplaced");
        await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            email,
            shape: x =>
            {
                if (profile == null)
                {
                    x.Remove("CountryRegion");
                }
                else
                {
                    x["CountryRegion"] = profile;
                }
            }
        );

        var cookie = await ApiSessions.SignInAsync(api, client, email);

        var user = (await ApiClients.FindUserAsync(_mongo.ConnectionString, email))!;
        Assert.False(user.Contains("HomeTenantId"));
        Assert.False(user.Contains("Memberships"));
        Assert.Equal(HttpStatusCode.OK, (await ApiSessions.GetAsync(client, "/api/me", cookie)).StatusCode);
    }

    [Fact]
    public async Task A_country_without_an_ISO_code_cannot_make_a_Tenant_and_leaves_the_person_unplaced()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var name = $"Noisoland {Guid.NewGuid():N}";
        await CountrySeed.AddAsync(_mongo.ConnectionString, name, null);
        var email = UserSeed.NewEmail("noiso");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email, shape: x => x["CountryRegion"] = name);

        await ApiSessions.SignInAsync(api, client, email);

        var user = (await ApiClients.FindUserAsync(_mongo.ConnectionString, email))!;
        Assert.False(user.Contains("HomeTenantId"));
    }

    [Fact]
    public async Task A_person_who_has_a_home_Tenant_is_not_moved_when_their_profile_country_changes_or_their_memberships_touched()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var elsewhere = await NewCountryAsync();
        var email = UserSeed.NewEmail("settled");
        await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            email,
            shape: x =>
            {
                x["CountryRegion"] = elsewhere.Name;
                x["HomeTenantId"] = "country-home";
                x["Memberships"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["TenantId"] = "country-home",
                        ["Roles"] = new BsonArray { "main-operator" },
                    },
                    new BsonDocument { ["TenantId"] = "country-away", ["Roles"] = new BsonArray() },
                };
            }
        );

        await ApiSessions.SignInAsync(api, client, email);

        var user = (await ApiClients.FindUserAsync(_mongo.ConnectionString, email))!;
        Assert.Equal("country-home", user["HomeTenantId"].AsString);
        Assert.Equal(2, user["Memberships"].AsBsonArray.Count);
        Assert.Equal("main-operator", user["Memberships"][0]["Roles"][0].AsString);
        Assert.Null(
            await ApiClients.FindTenantAsync(_mongo.ConnectionString, $"country-{elsewhere.IsoCode.ToLowerInvariant()}")
        );
    }

    ApiFactory NewApi()
    {
        return new ApiFactory(_mongo.ConnectionString);
    }

    async Task<SeededCountry> NewCountryAsync()
    {
        var name = $"Testland {Guid.NewGuid():N}";
        var iso = CountrySeed.UniqueIsoCode();
        var nf = $"N{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        await CountrySeed.AddAsync(_mongo.ConnectionString, name, iso, nf);
        return new SeededCountry(name, iso, nf);
    }

    sealed class SeededCountry
    {
        public SeededCountry(string name, string isoCode, string nfCode)
        {
            Name = name;
            IsoCode = isoCode;
            NfCode = nfCode;
        }

        public string Name { get; }
        public string IsoCode { get; }
        public string NfCode { get; }
    }
}
