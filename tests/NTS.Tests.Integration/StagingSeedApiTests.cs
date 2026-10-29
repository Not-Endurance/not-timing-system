using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.Staging;

namespace NTS.Tests.Integration;

/// <summary>
/// #607, what <c>seed-staging</c> makes is what a person who tries the platform on staging meets: the Api serves the Event,
/// each seeded person signs in with a code sent to the address, and the Officials and Operators send Snapshots to it while
/// the Tenant Root, who is not linked to the Event, may not. The real host over the database the seed made.
/// </summary>
public sealed class StagingSeedApiTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public StagingSeedApiTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    static DateTimeOffset Today => new(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);

    [Fact]
    public async Task The_Api_serves_the_seeded_Event_and_who_the_seed_linked_to_it_sends_Snapshots_to_it()
    {
        var (eventId, api, client) = await SeededApiAsync();
        await using var _ = api;
        using var __ = client;

        var live = await ApiSessions.GetAsync(client, "/api/events/live", null);
        var listed = (await ApiSessions.ReadJsonAsync(live))
            .GetProperty("data")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetString());
        Assert.Contains(eventId.ToString(), listed);
        var participations = await ApiSessions.GetAsync(
            client,
            $"/api/participations?filter=eventId eq {eventId}",
            null
        );
        Assert.Equal(12, (await ApiSessions.ReadJsonAsync(participations)).GetProperty("data").GetArrayLength());

        var arrive = Today.AddHours(8);
        var steward = await PageOfAsync(api, client, "official.one@example.test");
        var operatorPage = await PageOfAsync(api, client, "operator@example.test");
        var main = await PageOfAsync(api, client, "main@example.test");
        var root = await PageOfAsync(api, client, "root@example.test");

        Assert.Equal(HttpStatusCode.Created, (await SendAsync(steward, eventId, 1, arrive)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(operatorPage, eventId, 2, arrive)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(main, eventId, 3, arrive)).StatusCode);
        var refused = await SendAsync(root, eventId, 4, arrive);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode); // a Tenant Root is not linked to the Event it made no grant for
    }

    [Fact]
    public async Task The_seeded_accounts_have_the_roles_the_seed_gave_them_and_the_Tenant_is_operational()
    {
        var (eventId, api, client) = await SeededApiAsync();
        await using var _ = api;
        using var __ = client;
        var root = await PageOfAsync(api, client, "root@example.test");
        var official = await PageOfAsync(api, client, "official.two@example.test");

        var rootMe = await ApiSessions.ReadJsonAsync(await root.GetAsync("/api/me"));
        var officialMe = await ApiSessions.ReadJsonAsync(await official.GetAsync("/api/me"));

        var memberships = rootMe
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("memberships")
            .EnumerateArray()
            .ToArray();
        Assert.Equal("country-bg", Assert.Single(memberships).GetProperty("tenantId").GetString());
        Assert.Equal(
            ["tenant-root"],
            Assert.Single(memberships).GetProperty("roles").EnumerateArray().Select(x => x.GetString())
        );
        Assert.Equal(
            "country-bg",
            officialMe.GetProperty("data").GetProperty("attributes").GetProperty("homeTenantId").GetString()
        );
        var capabilities = await ApiSessions.ReadJsonAsync(await root.GetAsync($"/api/events/{eventId}/capabilities"));
        Assert.True(
            capabilities.GetProperty("data").GetProperty("attributes").GetProperty("canCreateEvents").GetBoolean()
        );
        var tenant = await ApiSessions.ReadJsonAsync(await root.GetAsync("/api/tenants/country-bg"));
        Assert.True(tenant.GetProperty("data").GetProperty("attributes").GetProperty("isOperational").GetBoolean());
    }

    [Fact]
    public async Task The_Main_Operator_of_the_seeded_Event_may_reset_it()
    {
        var (eventId, api, client) = await SeededApiAsync();
        await using var _ = api;
        using var __ = client;
        var main = await PageOfAsync(api, client, "main@example.test");

        var reset = await main.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/events/{eventId}"));

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(UserSeed.DATABASE);
        Assert.Equal(
            0,
            await database.GetCollection<BsonDocument>("event_participations").CountDocumentsAsync(new BsonDocument())
        );
        Assert.Equal(
            0,
            await database.GetCollection<BsonDocument>("event_informations").CountDocumentsAsync(new BsonDocument())
        );
        Assert.Equal(
            1,
            await database.GetCollection<BsonDocument>("configure_events").CountDocumentsAsync(new BsonDocument())
        ); // the Setup is what is left
    }

    static Task<HttpResponseMessage> SendAsync(PageClient page, Guid eventId, int number, DateTimeOffset time)
    {
        return page.WriteAsync(
            HttpMethod.Post,
            "/api/snapshots",
            "snapshots",
            new
            {
                eventId,
                number,
                kind = "Arrive",
                time,
            },
            id: Guid.NewGuid().ToString()
        );
    }

    static async Task<PageClient> PageOfAsync(ApiFactory api, HttpClient client, string email)
    {
        var page = new PageClient(client);
        page.Set(await ApiSessions.SignInAsync(api, client, email));
        return page;
    }

    async Task<(Guid EventId, ApiFactory Api, HttpClient Client)> SeededApiAsync()
    {
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(UserSeed.DATABASE);
        var report = await StagingSeed.Run(
            database,
            new StagingSeedOptions
            {
                Apply = true,
                Environment = "Staging",
                TenantRoot = "root@example.test",
                MainOperator = "main@example.test",
                Officials =
                [
                    ("official.one@example.test", OfficialRole.Steward),
                    ("official.two@example.test", OfficialRole.GroundJury),
                ],
                Operators = ["operator@example.test"],
            },
            DateTimeOffset.UtcNow
        );
        var api = new ApiFactory(_mongo.ConnectionString);
        return (report.EventId, api, api.CreateClient());
    }
}
