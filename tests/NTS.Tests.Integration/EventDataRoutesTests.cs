using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Core.Models;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Rankings, Officials and Handouts of an Event as the Api serves them (#604), the way the Participations are (see
/// <c>ParticipationRoutesTests</c>): flat, with the Event as an attribute and the Event at the head of a list's filter, and
/// read by anybody. What each family keeps to the server is not shown: the Official's linked account in particular, because
/// a visitor must not learn who is behind a name. The documents are seeded straight into MongoDB.
/// </summary>
public sealed class EventDataRoutesTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public EventDataRoutesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Rankings_of_an_Event_are_listed_and_read_by_anybody_with_the_Participations_they_count()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var first = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, Guid.NewGuid());
        var second = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 2, Guid.NewGuid());
        var ranking = IntegrationPayloadFactory.Ranking(eventId, [first, second], Guid.NewGuid(), "CEI 1*");
        await EventSeed.RankingAsync(_mongo.ConnectionString, tenant, ranking);

        var list = await anonymous.GetAsync($"/api/rankings?filter=eventId eq {eventId}");
        var read = await anonymous.GetAsync($"/api/rankings/{ranking.Id}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var listed = Assert.Single((await ApiSessions.ReadJsonAsync(list)).GetProperty("data").EnumerateArray());
        Assert.Equal("rankings", listed.GetProperty("type").GetString());
        Assert.Equal(ranking.Id.ToString(), listed.GetProperty("id").GetString());
        var attributes = (await ApiSessions.ReadJsonAsync(read)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("CEI 1*", attributes.GetProperty("name").GetString());
        Assert.Equal(eventId.ToString(), attributes.GetProperty("eventId").GetString());
        var entries = attributes.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(
            [first.Id.ToString(), second.Id.ToString()],
            entries.Select(x => x.GetProperty("participationId").GetString())
        );
        Assert.Equal([1, 2], entries.Select(x => x.GetProperty("rank").GetInt32()));
        Assert.False(attributes.TryGetProperty("isDeleted", out _));
        Assert.False(attributes.TryGetProperty("deletedVersion", out _));
    }

    [Fact]
    public async Task The_Officials_of_an_Event_are_shown_by_name_and_role_and_never_by_the_account_they_are_linked_to()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var linked = IntegrationPayloadFactory.Official(eventId, TestId.Of(77), Guid.NewGuid());
        var unlinked = IntegrationPayloadFactory.Official(eventId, null, Guid.NewGuid());
        await EventSeed.OfficialAsync(_mongo.ConnectionString, tenant, linked);
        await EventSeed.OfficialAsync(_mongo.ConnectionString, tenant, unlinked);

        var list = await anonymous.GetAsync($"/api/officials?filter=eventId eq {eventId}");
        var read = await anonymous.GetAsync($"/api/officials/{linked.Id}");
        var asked = await anonymous.GetAsync(
            $"/api/officials?filter=eventId eq {eventId} and userId eq {TestId.Of(77)}"
        );

        var shown = (await ApiSessions.ReadJsonAsync(list)).GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(2, shown.Count);
        Assert.All(
            shown,
            x =>
            {
                var attributes = x.GetProperty("attributes");
                Assert.Equal("Integration Official", attributes.GetProperty("name").GetString());
                Assert.Equal("GroundJury", attributes.GetProperty("role").GetString());
                Assert.False(attributes.TryGetProperty("userId", out _));
            }
        );
        Assert.False(
            (await ApiSessions.ReadJsonAsync(read))
                .GetProperty("data")
                .GetProperty("attributes")
                .TryGetProperty("userId", out _)
        );
        Assert.Equal(HttpStatusCode.BadRequest, asked.StatusCode); // what is not shown cannot be asked for
        Assert.Equal("invalid-filter", await ErrorCodeAsync(asked));
    }

    [Fact]
    public async Task The_Handouts_of_an_Event_are_listed_and_read_by_anybody_and_found_by_their_Participation()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var first = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, Guid.NewGuid());
        var second = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 2, Guid.NewGuid());
        var handout = IntegrationPayloadFactory.Handout(first, Guid.NewGuid());
        await EventSeed.HandoutAsync(_mongo.ConnectionString, tenant, handout);
        await EventSeed.HandoutAsync(
            _mongo.ConnectionString,
            tenant,
            IntegrationPayloadFactory.Handout(second, Guid.NewGuid())
        );

        var ofFirst = await anonymous.GetAsync(
            $"/api/handouts?filter=eventId eq {eventId} and participationId eq {first.Id}"
        );
        var read = await anonymous.GetAsync($"/api/handouts/{handout.Id}");

        var found = Assert.Single((await ApiSessions.ReadJsonAsync(ofFirst)).GetProperty("data").EnumerateArray());
        Assert.Equal(handout.Id.ToString(), found.GetProperty("id").GetString());
        var attributes = (await ApiSessions.ReadJsonAsync(read)).GetProperty("data").GetProperty("attributes");
        Assert.Equal(first.Id.ToString(), attributes.GetProperty("participationId").GetString());
        Assert.Equal(eventId.ToString(), attributes.GetProperty("eventId").GetString());
    }

    [Theory]
    [InlineData("participations")]
    [InlineData("rankings")]
    [InlineData("officials")]
    [InlineData("handouts")]
    public async Task Every_family_asks_for_its_Event_finds_nothing_for_an_Event_that_is_not_there_and_no_row_in_another(
        string family
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);

        var unnamed = await anonymous.GetAsync($"/api/{family}");
        var elsewhere = await anonymous.GetAsync($"/api/{family}?filter=eventId eq {Guid.NewGuid()}");
        var missing = await anonymous.GetAsync($"/api/{family}/{Guid.NewGuid()}");
        var malformed = await anonymous.GetAsync($"/api/{family}/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, unnamed.StatusCode);
        Assert.Equal("event-required", await ErrorCodeAsync(unnamed));
        Assert.Equal(HttpStatusCode.OK, elsewhere.StatusCode);
        Assert.Empty((await ApiSessions.ReadJsonAsync(elsewhere)).GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    [Fact]
    public async Task What_an_Event_that_has_ended_kept_is_read_by_anybody_as_it_was_while_it_ran()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, Guid.NewGuid());
        var ranking = IntegrationPayloadFactory.Ranking(eventId, [participation], Guid.NewGuid(), "CEI 1*");
        var official = IntegrationPayloadFactory.Official(eventId, null, Guid.NewGuid());
        var handout = IntegrationPayloadFactory.Handout(participation, Guid.NewGuid());
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation, version: 4);
        await EventSeed.RankingAsync(_mongo.ConnectionString, tenant, ranking);
        await EventSeed.OfficialAsync(_mongo.ConnectionString, tenant, official);
        await EventSeed.HandoutAsync(_mongo.ConnectionString, tenant, handout);

        foreach (
            var (family, id) in new[]
            {
                ("participations", participation.Id),
                ("rankings", ranking.Id),
                ("officials", official.Id),
                ("handouts", handout.Id),
            }
        )
        {
            var list = await anonymous.GetAsync($"/api/{family}?filter=eventId eq {eventId}");
            var read = await anonymous.GetAsync($"/api/{family}/{id}");

            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Equal(
                [id.ToString()],
                (await ApiSessions.ReadJsonAsync(list))
                    .GetProperty("data")
                    .EnumerateArray()
                    .Select(x => x.GetProperty("id").GetString())
            );
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }
    }

    [Theory]
    [InlineData("participations")]
    [InlineData("rankings")]
    [InlineData("officials")]
    [InlineData("handouts")]
    public async Task No_list_takes_a_Tenant_or_a_flag_for_every_Tenant_as_a_parameter_because_the_Tenant_is_the_Events(
        string family
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);

        foreach (var parameter in new[] { "tenantId=country-xx", "tenant=country-xx", "allTenants=true", "all=true" })
        {
            var response = await anonymous.GetAsync($"/api/{family}?filter=eventId eq {eventId}&{parameter}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("unsupported-parameter", await ErrorCodeAsync(response));
        }
    }

    [Fact]
    public async Task The_rows_of_one_Event_are_not_in_the_list_of_another_even_in_the_same_Tenant()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mine = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var other = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        await EventSeed.OfficialAsync(
            _mongo.ConnectionString,
            tenant,
            IntegrationPayloadFactory.Official(other, null, Guid.NewGuid())
        );
        var shown = IntegrationPayloadFactory.Official(mine, null, Guid.NewGuid());
        await EventSeed.OfficialAsync(_mongo.ConnectionString, tenant, shown);

        var response = await anonymous.GetAsync($"/api/officials?filter=eventId eq {mine}");
        var crafted = await anonymous.GetAsync($"/api/officials?filter=eventId eq {mine} or eventId eq {other}");

        Assert.Equal(
            [shown.Id.ToString()],
            (await ApiSessions.ReadJsonAsync(response))
                .GetProperty("data")
                .EnumerateArray()
                .Select(x => x.GetProperty("id").GetString())
        );
        Assert.Equal(HttpStatusCode.BadRequest, crafted.StatusCode); // the Event is joined to the rest with and
        Assert.Equal("event-required", await ErrorCodeAsync(crafted));
    }

    [Theory]
    [InlineData("clubs")]
    [InlineData("event_informations")]
    [InlineData("event_grants")]
    public async Task A_row_is_found_by_its_id_only_in_a_collection_that_an_Event_keeps_and_the_Api_serves(
        string collection
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var reads = api.Services.GetRequiredService<CrossTenantReads>();

        await Assert.ThrowsAsync<ArgumentException>(
            () => reads.FindEventRowAsync<ParticipationModel>(collection, Guid.NewGuid(), CancellationToken.None)
        );
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
