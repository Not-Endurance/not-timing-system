using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// Starting an Event (#628, ADR-0006, ADR-0007, ADR-0012): <c>POST /api/events</c> names the Setup the Event is made from,
/// and the Event is its Core document with the copies it made: its Officials and Operators, and the Participations and
/// Rankings of its competitions. The Event keeps the Tenant and the Main Operator of its Setup and the rules of its Tenant
/// as they are at the start. Starting is configuring the Event, which is the Main Operator's, and only before it starts;
/// starting the same Event again is the Event that was started.
/// </summary>
public sealed class EventStartTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public EventStartTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Main_Operator_starts_the_Event_from_its_Setup_and_the_Event_keeps_what_the_Setup_has_and_the_rules_of_the_Tenant()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var rules = new BsonDocument { { "OnlyAverageLoopSpeed", true }, { "RankerCode", "bg" } };
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true, rules: rules);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);

        var response = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/events/{id}", response.Headers.Location?.ToString());
        var resource = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal("events", resource.GetProperty("type").GetString());
        Assert.Equal(id.ToString(), resource.GetProperty("id").GetString());
        var attributes = resource.GetProperty("attributes");
        Assert.Equal("Full Setup", attributes.GetProperty("name").GetString());
        Assert.Equal("Sofia", attributes.GetProperty("location").GetString());
        Assert.Equal("FEI42", attributes.GetProperty("feiShowId").GetString());
        Assert.Equal("BG", attributes.GetProperty("country").GetProperty("isoCode").GetString());
        Assert.Equal(tenant, attributes.GetProperty("tenantId").GetString());
        Assert.True(attributes.GetProperty("isLive").GetBoolean());
        Assert.True(attributes.GetProperty("regionalRules").GetProperty("onlyAverageLoopSpeed").GetBoolean());
        Assert.Equal("bg", attributes.GetProperty("regionalRules").GetProperty("rankerCode").GetString());
        var core = (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal(tenant, core["TenantId"].AsString);
        Assert.Equal(mainOperator.Id, core["MainOperatorId"].AsGuid);
        Assert.Equal("Full Setup", core["Name"].AsString);
        Assert.Equal(new DateTime(2030, 5, 21, 23, 59, 59, DateTimeKind.Utc), core["EndDay"].ToUniversalTime());
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_officials", id));
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_operators", id));
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", id));
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_rankings", id));
        Assert.Equal(0, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_handouts", id));
        await AssertEventScopedAsync(tenant, id);
        Assert.NotNull(await EventSeed.SetupOfAsync(_mongo.ConnectionString, id));
    }

    [Fact]
    public async Task Starting_the_same_Event_again_is_the_Event_that_was_started_and_adds_nothing()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var first = await StartAsync(mainOperator, id);
        var coreBefore = (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!;

        var again = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(
            id.ToString(),
            (await ApiSessions.ReadJsonAsync(again)).GetProperty("data").GetProperty("id").GetString()
        );
        Assert.Equal(coreBefore, await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", id));
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_rankings", id));
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_officials", id));
    }

    [Fact]
    public async Task Starting_is_the_Main_Operators_and_the_Developers_before_the_start_and_nobody_elses()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var refused = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var byDeveloper = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);

        foreach (var who in new[] { root, member })
        {
            var response = await StartAsync(who, refused);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("not-main-operator", await ErrorCodeAsync(response));
        }

        var started = await StartAsync(developer, byDeveloper);
        var anonymous = await client.PostAsync("/api/events", new StringContent("{}"));

        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, refused));
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task A_Tenant_Root_who_assigned_itself_as_Main_Operator_starts_the_Event_as_the_Main_Operator_does()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, colleague.Id);
        var before = await StartAsync(root, id);

        var assigned = await root.Page.WriteAsync(
            HttpMethod.Post,
            $"/api/events/{id}/actions/assign-main-operator",
            "main-operators",
            new { accountId = root.Id }
        );
        var after = await StartAsync(root, id);

        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        Assert.Equal(HttpStatusCode.Created, after.StatusCode);
        Assert.Equal(root.Id, (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!["MainOperatorId"].AsGuid);
    }

    [Fact]
    public async Task An_Event_that_has_ended_is_not_started_again_by_anybody()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);

        var response = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_Setup_that_cannot_start_is_refused_with_what_is_wrong_and_nothing_is_written()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Empty Setup");

        var response = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = (await ApiSessions.ReadJsonAsync(response)).GetProperty("errors")[0];
        Assert.Equal("invalid-setup", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("detail").GetString()));
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
        Assert.Equal(0, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", id));
    }

    [Fact]
    public async Task A_Setup_that_has_some_of_the_FEI_configuration_and_not_all_of_it_is_refused_with_what_is_missing()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        await EventSeed
            .Setups(_mongo.ConnectionString)
            .UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", EventSeed.Binary(id)),
                Builders<BsonDocument>.Update.Unset("Competitions.0.FeiRule")
            );

        var response = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = (await ApiSessions.ReadJsonAsync(response)).GetProperty("errors")[0];
        Assert.Equal("incomplete-fei-configuration", error.GetProperty("code").GetString());
        Assert.Contains("CEI 1*", error.GetProperty("detail").GetString());
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
    }

    [Fact]
    public async Task The_document_names_the_Setup_it_starts_and_nothing_else_and_the_Setup_has_to_be_there()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);

        var withoutId = await mainOperator.Page.WriteAsync(HttpMethod.Post, "/api/events", "events", new { });
        var notAnId = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/events",
            "events",
            new { },
            id: "spring"
        );
        var missing = await StartAsync(mainOperator, Guid.NewGuid());
        var withAttributes = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/events",
            "events",
            new { name = "Another name" },
            id: id.ToString()
        );
        var wrongType = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/events",
            "configure-events",
            new { },
            id: id.ToString()
        );

        Assert.Equal(HttpStatusCode.BadRequest, withoutId.StatusCode);
        Assert.Equal("id-required", await ErrorCodeAsync(withoutId));
        Assert.Equal(HttpStatusCode.BadRequest, notAnId.StatusCode);
        Assert.Equal("invalid-id", await ErrorCodeAsync(notAnId));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withAttributes.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(withAttributes));
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(wrongType));
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
    }

    [Fact]
    public async Task A_Start_that_fails_part_way_leaves_no_Event_behind_and_the_Event_starts_once_the_cause_is_gone()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var participations = RegistrySeed.Collection(_mongo.ConnectionString, "event_participations");
        var index = await participations.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                new BsonDocument("Category", 1),
                new CreateIndexOptions<BsonDocument>
                {
                    Unique = true,
                    PartialFilterExpression = new BsonDocument("TenantId", tenant),
                    Name = "only-one-category-" + Guid.NewGuid().ToString("N"),
                }
            )
        );
        var foreign = EventSeed.Binary(Guid.NewGuid());
        await participations.InsertOneAsync(
            new BsonDocument
            {
                { "_id", foreign },
                { "EventId", EventSeed.Binary(Guid.NewGuid()) },
                { "TenantId", tenant },
                { "Category", "Senior" },
            }
        );
        try
        {
            // The second Participation of the category is refused by the database, after the Officials and the Operators.
            try
            {
                await StartAsync(mainOperator, id);
            }
            catch (Exception)
            {
                // A failure nobody planned for is the host's to answer for: what matters is what it leaves.
            }

            Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
            foreach (var collection in EventSeed.EVENT_COLLECTIONS)
            {
                Assert.Equal(0, await EventSeed.CountOfAsync(_mongo.ConnectionString, collection, id));
            }
        }
        finally
        {
            await participations.DeleteOneAsync(new BsonDocument("_id", foreign));
            await participations.Indexes.DropOneAsync(index);
        }

        var again = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", id));
    }

    ApiFactory NewApi()
    {
        return new ApiFactory(_mongo.ConnectionString, time: new FakeTimeProvider(NOW));
    }

    async Task AssertEventScopedAsync(string tenant, Guid id)
    {
        foreach (var collection in EventSeed.EVENT_COLLECTIONS.Take(4))
        {
            var documents = await RegistrySeed
                .Collection(_mongo.ConnectionString, collection)
                .Find(new BsonDocument("EventId", EventSeed.Binary(id)))
                .ToListAsync();
            Assert.NotEmpty(documents);
            Assert.All(documents, x => Assert.Equal(tenant, x["TenantId"].AsString));
        }
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
