using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;

namespace NTS.Tests.Integration;

/// <summary>
/// The Api and the Functions API share one MongoDB until the Functions API is retired (#647), so what the Api writes about
/// an Event, its Tenant and its Main Operator, has to read through the Functions API and survive what it writes back, and
/// what the Functions API stores about a started Event has to carry them and the rules it copied (ADR-0012, #643). The
/// Functions API edits a Setup field by field, so what it does not know is not lost.
/// </summary>
public sealed class ApiAndFunctionsCoexistTests : IClassFixture<NtsIntegrationFixture>
{
    readonly NtsIntegrationFixture _fixture;

    public ApiAndFunctionsCoexistTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task An_Event_the_Api_made_is_read_by_the_Functions_API_and_an_edit_through_it_keeps_the_Tenant_and_the_Main_Operator()
    {
        await using var api = new ApiFactory(_fixture.MongoConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_fixture.MongoConnectionString, "Bulgaria", withCountry: true);
        var root = await SignedInAsync(api, client, _fixture.MongoConnectionString, tenant, TenantRootOf(tenant));
        var created = await root.Page.WriteAsync(
            HttpMethod.Post,
            "/api/configure-events",
            "configure-events",
            new
            {
                name = "Spring Ride",
                location = "Sofia",
                feiShowId = "FEI42",
            }
        );
        var id = Guid.Parse(
            (await ApiSessions.ReadJsonAsync(created)).GetProperty("data").GetProperty("id").GetString()!
        );
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);

        var read = await functionsApi.ReadSetupConfigureEvent(id);

        Assert.Equal("Spring Ride", read.Name);
        Assert.Equal("Sofia", read.Location);
        Assert.Equal("FEI42", read.FeiShowId);
        Assert.Equal("Bulgaria", read.Country.Name);
        Assert.Equal(tenant, read.TenantId);
        Assert.Equal(root.Id, read.MainOperatorId);
        Assert.Empty(read.Competitions);

        // Judge edits the Setup as it always did, with the models it has: the constant Tenant, and no Main Operator.
        var edited = new SetupConfigureEvent("Renamed Ride", "Plovdiv", read.Country, null, [], [], [], [], id: id);
        await functionsApi.UpdateSetupConfigureEvent(edited);

        var after = await functionsApi.ReadSetupConfigureEvent(id);
        Assert.Equal("Renamed Ride", after.Name);
        Assert.Equal("Plovdiv", after.Location);
        Assert.Equal(tenant, after.TenantId);
        Assert.Equal(root.Id, after.MainOperatorId);
        var stored = (await EventSeed.SetupOfAsync(_fixture.MongoConnectionString, id))!;
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal(root.Id, stored["MainOperatorId"].AsGuid);
    }

    [Fact]
    public async Task A_started_Event_is_stored_by_the_Functions_API_with_its_Tenant_Main_Operator_and_rules_and_reads_back_with_them()
    {
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        var eventId = Guid.NewGuid();
        var mainOperator = Guid.NewGuid();
        var country = new Country(Guid.NewGuid(), "Bulgaria", "BG", "BUL", "bg-BG");
        var started = new EventInformation(
            country,
            "Started Ride",
            "Sofia",
            new EventSpan(DateTimeOffset.UtcNow.Date, DateTimeOffset.UtcNow.Date.AddDays(1)),
            null,
            eventId,
            new RegionalRules(true, "BG"),
            "country-bg",
            mainOperator
        );

        await functionsApi.Create(started);

        var stored = await new MongoClient(_fixture.MongoConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>("event_informations")
            .Find(new BsonDocument("_id", EventSeed.Binary(eventId)))
            .FirstAsync();
        Assert.Equal("country-bg", stored["TenantId"].AsString);
        Assert.Equal(mainOperator, stored["MainOperatorId"].AsGuid);
        var rules = stored["RegionalRules"].AsBsonDocument;
        Assert.True(rules["OnlyAverageLoopSpeed"].AsBoolean);
        Assert.Equal("BG", rules["RankerCode"].AsString);
        var read = await functionsApi.ReadEventInformation(eventId);
        Assert.Equal("country-bg", read.TenantId);
        Assert.Equal(mainOperator, read.MainOperatorId);
        Assert.Equal(new RegionalRules(true, "BG"), read.RegionalRules);
    }

    [Fact]
    public async Task An_Event_stored_before_Tenants_reads_with_the_constant_Tenant_no_Main_Operator_and_no_rules()
    {
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        var eventId = Guid.NewGuid();
        await functionsApi.Create(IntegrationPayloadFactory.EventInformation(eventId));
        var stored = new MongoClient(_fixture.MongoConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>("event_informations");
        await stored.UpdateOneAsync(
            new BsonDocument("_id", EventSeed.Binary(eventId)),
            Builders<BsonDocument>.Update.Unset("MainOperatorId").Unset("RegionalRules").Unset("TenantId")
        );

        var read = await functionsApi.ReadEventInformation(eventId);

        Assert.Equal(Tenant.LEGACY_ID, read.TenantId);
        Assert.Null(read.MainOperatorId);
        Assert.Equal(RegionalRules.None, read.RegionalRules);
    }
}
