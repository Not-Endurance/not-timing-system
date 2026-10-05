using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// Resetting an Event (#628, ADR-0012): <c>DELETE /api/events/{id}</c> takes an Event back to its Setup, for its Main
/// Operator and only while it is Live. Everything the Event made when it started and everything that was kept for it
/// goes, and the Core document of the Event goes last, so that a reset that stopped half way is made again by making it
/// again. What belongs to another Event, and the Setup, stay.
/// </summary>
public sealed class EventResetTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public EventResetTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Main_Operator_resets_a_Live_Event_and_everything_it_made_and_everything_kept_for_it_goes()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var other = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Another Event");
        await StartAsync(mainOperator, id);
        await StartAsync(mainOperator, other);
        await EventSeed.KeepsAsync(_mongo.ConnectionString, tenant, id);
        await EventSeed.KeepsAsync(_mongo.ConnectionString, tenant, other);
        var otherBefore = await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", other);

        var response = await mainOperator.Page.DeleteAsync($"/api/events/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
        foreach (
            var collection in EventSeed.EVENT_COLLECTIONS.Concat(["event_user_sessions", "event_pending_snapshots"])
        )
        {
            Assert.Equal(0, await EventSeed.CountOfAsync(_mongo.ConnectionString, collection, id));
        }

        Assert.NotNull(await EventSeed.SetupOfAsync(_mongo.ConnectionString, id));
        Assert.NotNull(await EventSeed.CoreOfAsync(_mongo.ConnectionString, other));
        Assert.Equal(otherBefore, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", other));
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_handouts", other));
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_user_sessions", other));
        using var anonymous = ApiClients.Of(api);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/events/{id}")).StatusCode);
        var capabilities = await ApiSessions.ReadJsonAsync(
            await mainOperator.Page.GetAsync($"/api/events/{id}/capabilities")
        );
        Assert.Equal(
            "unstarted",
            capabilities.GetProperty("data").GetProperty("attributes").GetProperty("stage").GetString()
        );
    }

    [Fact]
    public async Task A_reset_Event_is_started_again_from_its_Setup()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        await StartAsync(mainOperator, id);
        await mainOperator.Page.DeleteAsync($"/api/events/{id}");

        var again = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", id));
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_rankings", id));
    }

    [Fact]
    public async Task An_Event_that_lost_part_of_what_it_made_is_reset_all_the_same_so_that_a_reset_that_stopped_is_made_again()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        await EventSeed.KeepsAsync(_mongo.ConnectionString, tenant, id);

        var response = await mainOperator.Page.DeleteAsync($"/api/events/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
        Assert.Equal(0, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", id));
    }

    [Fact]
    public async Task Only_the_Main_Operator_resets_and_only_while_the_Event_is_Live()
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
        await EventSeed.KeepsAsync(_mongo.ConnectionString, tenant, live);

        foreach (var who in new[] { root, member, developer })
        {
            var refused = await who.Page.DeleteAsync($"/api/events/{live}");
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Equal("not-main-operator", await ErrorCodeAsync(refused));
        }

        var ended = await mainOperator.Page.DeleteAsync($"/api/events/{historic}");
        var notStarted = await mainOperator.Page.DeleteAsync($"/api/events/{unstarted}");
        var anonymous = await client.DeleteAsync($"/api/events/{live}");
        var missing = await mainOperator.Page.DeleteAsync($"/api/events/{Guid.NewGuid()}");
        var notAnId = await mainOperator.Page.DeleteAsync("/api/events/spring-ride");

        Assert.Equal(HttpStatusCode.Conflict, ended.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(ended));
        Assert.Equal(HttpStatusCode.Conflict, notStarted.StatusCode);
        Assert.Equal("event-not-started", await ErrorCodeAsync(notStarted));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notAnId.StatusCode);
        Assert.NotNull(await EventSeed.CoreOfAsync(_mongo.ConnectionString, live));
        Assert.NotNull(await EventSeed.CoreOfAsync(_mongo.ConnectionString, historic));
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", live));
    }

    ApiFactory NewApi()
    {
        return new ApiFactory(_mongo.ConnectionString, time: new FakeTimeProvider(NOW));
    }

    static Task<HttpResponseMessage> StartAsync(Person person, Guid setup)
    {
        return person.Page.WriteAsync(HttpMethod.Post, "/api/events", "events", new { }, id: setup.ToString());
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
