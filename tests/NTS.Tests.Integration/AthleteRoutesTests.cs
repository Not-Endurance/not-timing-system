using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Athletes of a Tenant's registry (#603), served the way every family of reference data is (see
/// <c>ClubRoutesTests</c>). An Athlete embeds the Country it competes for and the Club it belongs to, and may be linked to
/// an account: that link holds the account's email, and no route shows it, takes it or lets it be filtered on, so a rider
/// cannot be found by the address of an account.
/// </summary>
public sealed class AthleteRoutesTests : IClassFixture<MongoFixture>
{
    const string RIDER_EMAIL = "rider.secret@example.test";

    readonly MongoFixture _mongo;

    public AthleteRoutesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task An_Athlete_is_made_with_the_Country_and_the_Club_it_embeds_and_read_back_as_they_were_sent()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = Guid.NewGuid();
        var country = new
        {
            id = Guid.NewGuid(),
            name = "Bulgaria",
            isoCode = "BG",
        };
        var club = new { id = Guid.NewGuid(), name = "Sofia Riders" };

        var created = await root.Page.WriteAsync(
            HttpMethod.Post,
            "/api/athletes",
            "athletes",
            new
            {
                name = "Иван Петров",
                nameEnglish = "Ivan Petrov",
                feiId = "10012345",
                country,
                club,
            },
            id: id.ToString()
        );
        var read = await root.Page.GetAsync($"/api/athletes/{id}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(read)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("Иван Петров", attributes.GetProperty("name").GetString());
        Assert.Equal("10012345", attributes.GetProperty("feiId").GetString());
        Assert.Equal("BG", attributes.GetProperty("country").GetProperty("isoCode").GetString());
        Assert.Equal(club.id.ToString(), attributes.GetProperty("club").GetProperty("id").GetString());
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "athletes", id))!;
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal("BG", stored["Country"]["IsoCode"].AsString);
        Assert.Equal("Sofia Riders", stored["Club"]["Name"].AsString);
    }

    [Fact]
    public async Task The_account_an_Athlete_is_linked_to_is_not_shown_by_any_route_and_does_not_leave_in_a_list()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await LinkedAthleteAsync(tenant);

        var item = await member.Page.GetAsync($"/api/athletes/{id}");
        var list = await member.Page.GetAsync("/api/athletes");

        foreach (var response in new[] { item, list })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(RIDER_EMAIL, body);
            Assert.DoesNotContain("\"user\"", body);
            Assert.Contains("Linked Rider", body);
        }
    }

    [Theory]
    [InlineData("user/email eq 'rider.secret@example.test'")]
    [InlineData("contains(user/email,'secret')")]
    [InlineData("user ne null")]
    public async Task The_account_link_cannot_be_asked_about_in_a_filter_and_so_does_not_tell_what_it_holds(
        string filter
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await LinkedAthleteAsync(tenant);

        var response = await member.Page.GetAsync("/api/athletes?filter=" + Uri.EscapeDataString(filter));
        var sorted = await member.Page.GetAsync("/api/athletes?sort=user/email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-filter", await ErrorCodeAsync(response));
        Assert.Equal(HttpStatusCode.BadRequest, sorted.StatusCode);
        Assert.Equal("invalid-sort", await ErrorCodeAsync(sorted));
    }

    [Fact]
    public async Task A_document_cannot_name_the_account_link_and_an_edit_of_the_Athlete_leaves_it_as_it_is()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await LinkedAthleteAsync(tenant);

        var naming = await ChangeAsync(root, id, new { user = new { email = "someone.else@example.test" } });
        var renaming = await ChangeAsync(root, id, new { name = "Renamed Rider" });

        Assert.Equal(HttpStatusCode.BadRequest, naming.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(naming));
        Assert.Equal(HttpStatusCode.OK, renaming.StatusCode);
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "athletes", id))!;
        Assert.Equal("Renamed Rider", stored["Name"].AsString);
        Assert.Equal(RIDER_EMAIL, stored["User"]["Email"].AsString);
    }

    [Theory]
    [InlineData("missing country")]
    [InlineData("feiId that is not a number")]
    [InlineData("no name")]
    public async Task An_Athlete_the_domain_does_not_accept_is_422_and_nothing_is_written(string reason)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var country = new
        {
            id = Guid.NewGuid(),
            name = "Bulgaria",
            isoCode = "BG",
        };
        object attributes = reason switch
        {
            "missing country" => new { name = "Ivan" },
            "feiId that is not a number" => new
            {
                name = "Ivan",
                country,
                feiId = "abc",
            },
            _ => new { country },
        };

        var response = await root.Page.WriteAsync(HttpMethod.Post, "/api/athletes", "athletes", attributes);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid-attribute", await ErrorCodeAsync(response));
        Assert.Equal(0, await RegistrySeed.CountAsync(_mongo.ConnectionString, "athletes", tenant));
    }

    [Fact]
    public async Task The_Athletes_of_the_Tenant_are_found_by_what_they_embed()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        var turkey = RegistrySeed.CountryOf("Turkey", "TR");
        var sofia = new BsonDocument
        {
            { "_id", RegistrySeed.Binary(Guid.NewGuid()) },
            { "TenantId", "nts" },
            { "Name", "Sofia Riders" },
        };
        await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Ivan",
            bulgaria,
            club: sofia,
            feiId: "10000001"
        );
        await RegistrySeed.AthleteAsync(_mongo.ConnectionString, tenant, "Mehmet", turkey, feiId: "10000002");
        await RegistrySeed.AthleteAsync(_mongo.ConnectionString, other, "Ivan", bulgaria, club: sofia);

        Assert.Equal(["Ivan"], Names(await ListAsync(member, "?filter=country/isoCode eq 'BG'")));
        Assert.Equal(["Mehmet"], Names(await ListAsync(member, "?filter=country/name eq 'Turkey'")));
        Assert.Equal(["Ivan"], Names(await ListAsync(member, "?filter=club/name eq 'Sofia Riders'")));
        Assert.Equal(["Ivan", "Mehmet"], Names(await ListAsync(member, "?sort=name")));
        Assert.Equal(["Mehmet"], Names(await ListAsync(member, "?filter=feiId eq '10000002'")));
    }

    [Fact]
    public async Task A_member_an_Athlete_has_not_is_null_in_a_filter_whatever_else_the_expression_says()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        await RegistrySeed.AthleteAsync(_mongo.ConnectionString, tenant, "Ivan", bulgaria, feiId: "10000001");
        await RegistrySeed.AthleteAsync(_mongo.ConnectionString, tenant, "Anna", bulgaria);

        Assert.Equal(["Ivan"], Names(await ListAsync(member, "?filter=contains(name,'a') and feiId ne null")));
        Assert.Equal(["Ivan"], Names(await ListAsync(member, "?filter=feiId ne null and contains(name,'a')")));
        Assert.Equal(["Anna"], Names(await ListAsync(member, "?filter=contains(name,'n') and feiId eq null")));
        Assert.Equal(["Anna"], Names(await ListAsync(member, "?filter=not (feiId ne null) and contains(name,'a')")));
        Assert.Equal(
            ["Anna", "Ivan"],
            Names(await ListAsync(member, "?filter=feiId eq null or name eq 'Ivan'")).Order()
        );
    }

    async Task<Guid> LinkedAthleteAsync(string tenant)
    {
        return await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Linked Rider",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            user: new BsonDocument
            {
                { "_id", RegistrySeed.Binary(Guid.NewGuid()) },
                { "TenantId", "nts" },
                { "Email", RIDER_EMAIL },
                { "Name", "Rider Secret" },
            }
        );
    }

    static Task<HttpResponseMessage> ChangeAsync(Person person, Guid id, object attributes)
    {
        return person.Page.WriteAsync(HttpMethod.Patch, $"/api/athletes/{id}", "athletes", attributes);
    }

    static async Task<JsonElement> ListAsync(Person person, string query)
    {
        var response = await person.Page.GetAsync("/api/athletes" + query);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{query} answered {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"
        );
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
