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
    static readonly DateTimeOffset NOW = DateTimeOffset.UtcNow;

    readonly MongoFixture _mongo;

    public EventResetTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Console_Operator_resets_a_Live_Event_and_everything_it_made_and_everything_kept_for_it_goes()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        var other = await EventSeed.FullSetupAsync(
            _mongo.ConnectionString,
            tenant,
            consoleOperator.Id,
            "Another Event"
        );
        await StartAsync(consoleOperator, id);
        await StartAsync(consoleOperator, other);
        await EventSeed.KeepsAsync(_mongo.ConnectionString, tenant, id);
        await EventSeed.KeepsAsync(_mongo.ConnectionString, tenant, other);
        var otherBefore = await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", other);

        var response = await consoleOperator.Page.DeleteAsync($"/api/events/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
        foreach (var collection in EventSeed.EVENT_COLLECTIONS.Concat(["event_user_sessions"]))
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
            await consoleOperator.Page.GetAsync($"/api/events/{id}/capabilities")
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
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        await StartAsync(consoleOperator, id);
        await consoleOperator.Page.DeleteAsync($"/api/events/{id}");

        var again = await StartAsync(consoleOperator, id);

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
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        await EventSeed.KeepsAsync(_mongo.ConnectionString, tenant, id);

        var response = await consoleOperator.Page.DeleteAsync($"/api/events/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
        Assert.Equal(0, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", id));
    }

    [Fact]
    public async Task A_reset_that_stops_part_way_leaves_the_Event_so_that_it_is_made_again()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        await StartAsync(consoleOperator, id);
        // Nothing can be removed from a view, which is how the reset stops after what the Event made is gone.
        var database = RegistrySeed.Collection(_mongo.ConnectionString, "event_user_sessions").Database;
        await database.DropCollectionAsync("event_user_sessions");
        await database.CreateViewAsync(
            "event_user_sessions",
            "event_officials",
            new EmptyPipelineDefinition<BsonDocument>()
        );
        try
        {
            try
            {
                await consoleOperator.Page.DeleteAsync($"/api/events/{id}");
            }
            catch (Exception)
            {
                // A failure nobody planned for is the host's to answer for: what matters is what it leaves.
            }

            Assert.NotNull(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
            Assert.Equal(0, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", id));
        }
        finally
        {
            await database.DropCollectionAsync("event_user_sessions");
        }

        var again = await consoleOperator.Page.DeleteAsync($"/api/events/{id}");

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
    }

    [Fact]
    public async Task Only_a_Console_Operator_and_Root_reset_and_only_while_the_Event_is_Live()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        await EventSeed.KeepsAsync(_mongo.ConnectionString, tenant, live);

        var refused = await member.Page.DeleteAsync($"/api/events/{live}");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("not-console-operator", await ErrorCodeAsync(refused));

        var ended = await consoleOperator.Page.DeleteAsync($"/api/events/{historic}");
        var notStarted = await consoleOperator.Page.DeleteAsync($"/api/events/{unstarted}");
        var anonymous = await client.DeleteAsync($"/api/events/{live}");
        var missing = await consoleOperator.Page.DeleteAsync($"/api/events/{Guid.NewGuid()}");
        var notAnId = await consoleOperator.Page.DeleteAsync("/api/events/spring-ride");

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

    [Theory]
    [InlineData("a Tenant Root")]
    [InlineData("the Developer")]
    public async Task Root_resets_a_Live_Event_it_is_not_assigned_to(string who)
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root =
            who == "a Tenant Root"
                ? await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant))
                : await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, NOW);
        await EventSeed.KeepsAsync(_mongo.ConnectionString, tenant, live);

        var reset = await root.Page.DeleteAsync($"/api/events/{live}");

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, live));
        Assert.Equal(0, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", live));
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
