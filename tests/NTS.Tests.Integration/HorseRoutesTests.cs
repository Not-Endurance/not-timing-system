using System.Net;
using System.Text.Json;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Horses of a Tenant's registry (#603), served the way every family of reference data is (see
/// <c>ClubRoutesTests</c> for what they all do alike): a Horse has a name, an English name and a FEI ID, the last two
/// optional, and what the document leaves out is what the row does not have.
/// </summary>
public sealed class HorseRoutesTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public HorseRoutesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Horse_is_made_with_its_members_and_read_back_as_they_were_sent()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = Guid.NewGuid();

        var created = await root.Page.WriteAsync(
            HttpMethod.Post,
            "/api/horses",
            "horses",
            new
            {
                name = "Бърза",
                nameEnglish = "Barza",
                feiId = "103AB45",
            },
            id: id.ToString()
        );
        var read = await root.Page.GetAsync($"/api/horses/{id}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(read)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("Бърза", attributes.GetProperty("name").GetString());
        Assert.Equal("Barza", attributes.GetProperty("nameEnglish").GetString());
        Assert.Equal("103AB45", attributes.GetProperty("feiId").GetString());
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "horses", id))!;
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal("Barza", stored["NameEnglish"].AsString);
    }

    [Fact]
    public async Task A_member_that_is_left_out_is_not_there_and_a_member_that_is_sent_as_null_is_taken_away()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Barza", "Barza", "103AB45");

        var untouched = await ChangeAsync(root, id, new { name = "Barza II" });
        var cleared = await ChangeAsync(root, id, new { nameEnglish = (string?)null });

        Assert.Equal(HttpStatusCode.OK, untouched.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "horses", id))!;
        Assert.Equal("Barza II", stored["Name"].AsString);
        Assert.Equal("103AB45", stored["FeiId"].AsString);
        Assert.False(stored.Contains("NameEnglish"));
        var attributes = (await ApiSessions.ReadJsonAsync(cleared)).GetProperty("data").GetProperty("attributes");
        Assert.False(attributes.TryGetProperty("nameEnglish", out _));
        Assert.Equal("103AB45", attributes.GetProperty("feiId").GetString());
    }

    [Fact]
    public async Task A_change_with_no_member_changes_nothing_and_answers_with_the_Horse()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Barza");

        var response = await ChangeAsync(root, id, new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "Barza",
            (await ApiSessions.ReadJsonAsync(response))
                .GetProperty("data")
                .GetProperty("attributes")
                .GetProperty("name")
                .GetString()
        );
    }

    [Fact]
    public async Task A_Horse_needs_a_name_and_the_name_may_not_be_taken_away()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Barza");

        var withoutName = await root.Page.WriteAsync(HttpMethod.Post, "/api/horses", "horses", new { feiId = "1" });
        var takenAway = await ChangeAsync(root, id, new { name = (string?)null });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, withoutName.StatusCode);
        Assert.Equal("invalid-attribute", await ErrorCodeAsync(withoutName));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, takenAway.StatusCode);
        Assert.Equal(
            "Barza",
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "horses", id))!["Name"].AsString
        );
    }

    [Theory]
    [InlineData("""{ "name": 12 }""")]
    [InlineData("""{ "name": "Barza II", "feiId": true }""")]
    [InlineData("""{ "name": ["Barza II"] }""")]
    public async Task A_member_of_another_type_than_it_has_is_refused_and_nothing_is_written(string attributes)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Barza");
        var document = JsonSerializer.Deserialize<JsonElement>(attributes);

        var made = await root.Page.WriteAsync(HttpMethod.Post, "/api/horses", "horses", document);
        var changed = await ChangeAsync(root, id, document);

        foreach (var response in new[] { made, changed })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("malformed-request", await ErrorCodeAsync(response));
        }

        Assert.Equal(1, await RegistrySeed.CountAsync(_mongo.ConnectionString, "horses", tenant));
        Assert.Equal(
            "Barza",
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "horses", id))!["Name"].AsString
        );
    }

    [Fact]
    public async Task The_Horses_of_the_Tenant_are_found_by_the_name_the_English_name_and_the_FEI_ID()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Бърза", "Barza", "103AB45");
        await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Вихър", "Vihar", "103AB46");
        await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Звезда");
        await RegistrySeed.HorseAsync(_mongo.ConnectionString, other, "Бърза", "Barza", "103AB45");

        Assert.Equal(["Бърза"], Names(await ListAsync(member, "?filter=feiId eq '103AB45'")));
        Assert.Equal(["Вихър"], Names(await ListAsync(member, "?filter=nameEnglish eq 'Vihar'")));
        Assert.Equal(["Бърза", "Вихър"], Names(await ListAsync(member, "?filter=startswith(feiId,'103AB')&sort=name")));
        Assert.Equal(["Звезда"], Names(await ListAsync(member, "?filter=nameEnglish eq null")));
    }

    [Fact]
    public async Task Whoever_may_edit_the_registry_removes_a_Horse_and_a_member_does_not()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Barza");

        var refused = await member.Page.DeleteAsync($"/api/horses/{id}");
        var removed = await root.Page.DeleteAsync($"/api/horses/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "horses", id));
    }

    static Task<HttpResponseMessage> ChangeAsync(Person person, Guid id, object attributes)
    {
        return person.Page.WriteAsync(HttpMethod.Patch, $"/api/horses/{id}", "horses", attributes);
    }

    static async Task<JsonElement> ListAsync(Person person, string query)
    {
        var response = await person.Page.GetAsync("/api/horses" + query);
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
