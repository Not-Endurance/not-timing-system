using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// Changing what an Event shows (#628, ADR-0007, ADR-0012): <c>PATCH /api/events/{id}</c> names the members to change
/// (its name, location, country, FEI show ID and days), for the Main Operator and while the Event is Live. The days are
/// whole days in the offset they are given in, as the span of an Event makes them, and an Event is over when its last day
/// is: it is not ended by changing its days. Who runs the Event, its Tenant and the rules it started with are not written.
/// </summary>
public sealed class EventChangeTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public EventChangeTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Main_Operator_changes_the_members_it_names_while_the_Event_is_Live_and_no_other()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var rules = new BsonDocument { { "OnlyAverageLoopSpeed", true } };
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true, rules: rules);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await StartedAsync(api, mainOperator, tenant);
        var before = (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!;

        var response = await ChangeAsync(mainOperator, id, new { name = "Renamed Ride", location = "Plovdiv" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("Renamed Ride", attributes.GetProperty("name").GetString());
        Assert.Equal("Plovdiv", attributes.GetProperty("location").GetString());
        Assert.Equal("FEI42", attributes.GetProperty("feiShowId").GetString());
        var after = (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal("Renamed Ride", after["Name"].AsString);
        Assert.Equal("Plovdiv", after["Location"].AsString);
        before["Name"] = after["Name"];
        before["Location"] = after["Location"];
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task A_member_that_is_sent_as_null_is_taken_away_and_one_the_Event_needs_is_not()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await StartedAsync(api, mainOperator, tenant);

        var cleared = await ChangeAsync(mainOperator, id, new { feiShowId = (string?)null });
        var nameless = await ChangeAsync(mainOperator, id, new { name = (string?)null });
        var blank = await ChangeAsync(mainOperator, id, new { location = "  " });

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.False((await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!.Contains("FeiShowId"));
        foreach (var refused in new[] { nameless, blank })
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            Assert.Equal("invalid-attribute", await ErrorCodeAsync(refused));
        }

        Assert.Equal("Full Setup", (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!["Name"].AsString);
    }

    [Fact]
    public async Task The_days_are_whole_days_in_the_offset_they_are_given_in()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await StartedAsync(api, mainOperator, tenant);

        var response = await ChangeAsync(
            mainOperator,
            id,
            new { startDay = "2030-05-20T10:30:00+03:00", endDay = "2030-06-02T10:30:00+03:00" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal(new DateTime(2030, 5, 19, 21, 0, 0, DateTimeKind.Utc), stored["StartDay"].ToUniversalTime());
        Assert.Equal(new DateTime(2030, 6, 2, 20, 59, 59, DateTimeKind.Utc), stored["EndDay"].ToUniversalTime());
    }

    [Fact]
    public async Task An_Event_is_not_ended_by_changing_its_days_because_it_is_over_when_its_last_day_is()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await StartedAsync(api, mainOperator, tenant);
        var before = (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!;

        var yesterday = await ChangeAsync(mainOperator, id, new { endDay = "2026-06-09T10:00:00Z" });
        var unchanged = (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!;
        var today = await ChangeAsync(mainOperator, id, new { endDay = "2026-06-10T10:00:00Z" });
        var tomorrow = await ChangeAsync(mainOperator, id, new { endDay = "2026-06-11T10:00:00Z" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, yesterday.StatusCode);
        Assert.Equal("invalid-attribute", await ErrorCodeAsync(yesterday));
        Assert.Equal(before, unchanged);
        Assert.Equal(HttpStatusCode.OK, today.StatusCode); // the last day is today: the Event is Live until its last second
        Assert.Equal(HttpStatusCode.OK, tomorrow.StatusCode);
        Assert.Equal(
            new DateTime(2026, 6, 11, 23, 59, 59, DateTimeKind.Utc),
            (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!["EndDay"].ToUniversalTime()
        );
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("mainOperatorId")]
    [InlineData("regionalRules")]
    [InlineData("isLive")]
    [InlineData("isActive")]
    [InlineData("id")]
    [InlineData("nothing")]
    public async Task What_the_server_owns_or_keeps_or_the_Event_has_not_is_not_taken_and_nothing_changes(string member)
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await StartedAsync(api, mainOperator, tenant);
        var before = (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!;

        var response = await ChangeAsync(
            mainOperator,
            id,
            new Dictionary<string, object> { [member] = "x", ["name"] = "Changed" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(response));
        Assert.Equal(before, await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
    }

    [Fact]
    public async Task Only_the_Main_Operator_changes_an_Event_and_only_while_it_is_Live()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);

        foreach (var who in new[] { root, member, developer })
        {
            var refused = await ChangeAsync(who, live, new { name = "Taken over" });
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Equal("not-main-operator", await ErrorCodeAsync(refused));
        }

        var ended = await ChangeAsync(mainOperator, historic, new { name = "Too late" });
        var notStarted = await ChangeAsync(mainOperator, unstarted, new { name = "Not an Event" });
        var anonymous = await client.PatchAsync($"/api/events/{live}", new StringContent("{}"));
        var mismatch = await mainOperator.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/events/{live}",
            "events",
            new { name = "Another" },
            id: Guid.NewGuid().ToString()
        );

        Assert.Equal(HttpStatusCode.Conflict, ended.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(ended));
        Assert.Equal(HttpStatusCode.NotFound, notStarted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Equal("id-mismatch", await ErrorCodeAsync(mismatch));
        Assert.NotEqual("Taken over", (await EventSeed.CoreOfAsync(_mongo.ConnectionString, live))!["Name"].AsString);
        Assert.NotEqual("Too late", (await EventSeed.CoreOfAsync(_mongo.ConnectionString, historic))!["Name"].AsString);
    }

    ApiFactory NewApi()
    {
        return new ApiFactory(_mongo.ConnectionString, time: new FakeTimeProvider(NOW));
    }

    async Task<Guid> StartedAsync(ApiFactory api, Person mainOperator, string tenant)
    {
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var started = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/events",
            "events",
            new { },
            id: id.ToString()
        );
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        return id;
    }

    static Task<HttpResponseMessage> ChangeAsync(Person person, Guid id, object attributes)
    {
        return person.Page.WriteAsync(HttpMethod.Patch, $"/api/events/{id}", "events", attributes);
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
