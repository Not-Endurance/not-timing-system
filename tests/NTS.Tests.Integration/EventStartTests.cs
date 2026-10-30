using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Reference;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Localization.NtsStrings;
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
    static readonly DateTimeOffset NOW = DateTimeOffset.UtcNow;

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

        // The Event that was started is the answer, whatever the Setup says by then: a repair of the data does not undo it.
        await EventSeed
            .Setups(_mongo.ConnectionString)
            .UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", EventSeed.Binary(id)),
                Builders<BsonDocument>.Update.Set("Competitions", new BsonArray())
            );
        var afterTheSetupChanged = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.OK, afterTheSetupChanged.StatusCode);
        Assert.Equal(coreBefore, await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
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
    public async Task A_combination_in_competitions_of_different_phases_is_refused_with_the_competitions_it_rides_in()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var key = Builders<BsonDocument>.Filter.Eq("_id", EventSeed.Binary(id));
        var setup = await EventSeed.Setups(_mongo.ConnectionString).Find(key).FirstAsync();
        var shorter = setup["Competitions"][0].AsBsonDocument.DeepClone().AsBsonDocument;
        shorter["_id"] = EventSeed.Binary(Guid.NewGuid());
        shorter["Name"] = "CEI 2*";
        shorter["Phases"] = new BsonArray(shorter["Phases"].AsBsonArray.Take(2));
        await EventSeed
            .Setups(_mongo.ConnectionString)
            .UpdateOneAsync(key, Builders<BsonDocument>.Update.Push("Competitions", shorter));

        var response = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = (await ApiSessions.ReadJsonAsync(response)).GetProperty("errors")[0];
        Assert.Equal("invalid-setup", error.GetProperty("code").GetString());
        var detail = error.GetProperty("detail").GetString()!;
        Assert.Contains("#101", detail);
        Assert.Contains("- CEI 1*: 20km/15min/40min | 20km/15min/40min | 10km/20min/final", detail);
        Assert.Contains("- CEI 2*: 20km/15min/40min | 20km/15min/40min", detail);
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
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

    [Theory]
    [InlineData("FeiEventId")]
    [InlineData("FeiEventCode")]
    [InlineData("FeiCompetitionId")]
    [InlineData("FeiRule")]
    [InlineData("FeiScheduleNumber")]
    public async Task What_is_missing_of_the_FEI_configuration_is_said_value_by_value_in_the_words_of_the_application(
        string kept
    )
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        (string Field, string Label)[] competitionValues =
        [
            ("FeiEventId", FEI_Event_ID_string),
            ("FeiEventCode", FEI_Event_Code_string),
            ("FeiCompetitionId", FEI_Competition_ID_string),
            ("FeiRule", FEI_Rule_string),
            ("FeiScheduleNumber", FEI_Schedule_Number_string),
        ];
        var unset = competitionValues
            .Where(x => x.Field != kept)
            .Select(x => Builders<BsonDocument>.Update.Unset($"Competitions.0.{x.Field}"))
            .Concat(
                [
                    Builders<BsonDocument>.Update.Unset("FeiShowId"),
                    Builders<BsonDocument>.Update.Unset("Competitions.0.Participations.0.Combination.Horse.FeiId"),
                    Builders<BsonDocument>.Update.Unset("Competitions.0.Participations.0.Combination.Athlete.FeiId"),
                ]
            );
        await EventSeed
            .Setups(_mongo.ConnectionString)
            .UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", EventSeed.Binary(id)),
                Builders<BsonDocument>.Update.Combine(unset)
            );

        var response = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = (await ApiSessions.ReadJsonAsync(response)).GetProperty("errors")[0];
        var lines = error
            .GetProperty("detail")
            .GetString()!
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(FEI_Show_ID_string, lines);
        Assert.Contains($"#101, Бърза: {FEI_ID_string}", lines);
        Assert.Contains($"#101, Иван Петров: {FEI_ID_string}", lines);
        foreach (var (field, label) in competitionValues)
        {
            Assert.Equal(field != kept, lines.Contains($"CEI 1*: {label}"));
        }
    }

    [Fact]
    public async Task A_Setup_that_an_Event_cannot_be_made_of_is_refused_as_what_it_is_and_nothing_is_written()
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
                Builders<BsonDocument>.Update.Unset("Location")
            );

        var response = await StartAsync(mainOperator, id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid-attribute", await ErrorCodeAsync(response));
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
        Assert.Equal(0, await EventSeed.CountOfAsync(_mongo.ConnectionString, "event_participations", id));
    }

    [Fact]
    public async Task A_Setup_of_a_Tenant_that_is_not_there_is_not_started_by_the_Developer_either()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, "nts", null);

        var response = await StartAsync(developer, id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("tenant-not-found", await ErrorCodeAsync(response));
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
            var answeredAsFailed = false;
            try
            {
                answeredAsFailed = (int)(await StartAsync(mainOperator, id)).StatusCode >= 500;
            }
            catch (Exception)
            {
                // A failure nobody planned for is the host's to answer for: it is not answered as if it had worked.
                answeredAsFailed = true;
            }

            Assert.True(answeredAsFailed);
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

    [Fact]
    public async Task Two_starts_that_get_there_together_make_one_Event_and_the_second_makes_nothing()
    {
        await using var api = NewApi();
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var id = Guid.NewGuid();

        // A plan is serialized here, before any host is built: the process has to hold the serializers of the Api first,
        // as the first date or Guid that is serialized fixes them for every test that runs after.
        ApiMongo.Configure();
        var (first, _) = EventStartPlans.Create(SetupFactory.Full(id), RegionalRules.None);
        var (second, _) = EventStartPlans.Create(SetupFactory.Full(id), RegionalRules.None);
        var starter = api.Services.GetRequiredService<EventStarter>();

        var winner = await starter.StartAsync(first!, tenant, CancellationToken.None);
        var late = await starter.StartAsync(second!, tenant, CancellationToken.None);

        Assert.True(winner);
        Assert.False(late);
        foreach (
            var collection in new[] { "event_officials", "event_operators", "event_participations", "event_rankings" }
        )
        {
            Assert.Equal(1, await EventSeed.CountOfAsync(_mongo.ConnectionString, collection, id));
        }
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
