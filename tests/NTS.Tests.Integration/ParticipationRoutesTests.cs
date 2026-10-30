using System.Net;
using System.Text.Json;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Participations of an Event as the Api serves them (#604, ADR-0006, ADR-0008, ADR-0012): flat, with the Event as an
/// attribute and the version of the document in the <c>meta</c> of each resource, read by anybody (ADR-0001) and written
/// by the Main Operator of a Live Event. The Event they belong to says which Tenant they are in, so a list is asked with
/// the Event, and no other Event's, nor another Tenant's, is in it. The documents are seeded straight into MongoDB.
/// </summary>
public sealed class ParticipationRoutesTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(2030, 5, 21, 8, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public ParticipationRoutesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Participations_of_an_Event_are_listed_to_anybody_with_the_version_of_each_in_its_meta()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var first = Ridden(eventId, 1);
        var second = Ridden(eventId, 2);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, first);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, second, version: 3);

        var response = await anonymous.GetAsync($"/api/participations?filter=eventId eq {eventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.api+json", response.Content.Headers.ContentType?.MediaType);
        var data = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(
            new[] { first.Id, second.Id }.Order().Select(x => x.ToString()),
            data.Select(x => x.GetProperty("id").GetString())
        );
        Assert.All(data, x => Assert.Equal("participations", x.GetProperty("type").GetString()));
        var one = data.Single(x => x.GetProperty("id").GetString() == second.Id.ToString());
        var attributes = one.GetProperty("attributes");
        Assert.Equal(eventId.ToString(), attributes.GetProperty("eventId").GetString());
        Assert.Equal(2, attributes.GetProperty("combination").GetProperty("number").GetInt32());
        Assert.Equal(2, attributes.GetProperty("phases").GetArrayLength());
        Assert.Equal(1, attributes.GetProperty("phases")[0].GetProperty("events").GetArrayLength());
        Assert.Equal(3, one.GetProperty("meta").GetProperty("version").GetInt32());
        Assert.Equal(
            0,
            data.Single(x => x.GetProperty("id").GetString() == first.Id.ToString())
                .GetProperty("meta")
                .GetProperty("version")
                .GetInt32()
        );
    }

    [Fact]
    public async Task What_the_document_keeps_for_the_server_is_not_shown_and_the_version_is_not_an_attribute()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var participation = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation, version: 2);

        var response = await anonymous.GetAsync($"/api/participations/{participation.Id}");

        var attributes = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
        foreach (var kept in new[] { "isDeleted", "deletedVersion", "version", "id" })
        {
            Assert.False(attributes.TryGetProperty(kept, out _), kept);
        }
    }

    [Fact]
    public async Task Another_Events_Participations_and_another_Tenants_are_not_in_the_list()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var otherTenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var sameTenant = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var elsewhere = await EventSeed.LiveAsync(_mongo.ConnectionString, otherTenant, null, DateTimeOffset.UtcNow);
        var mine = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, mine);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, Ridden(sameTenant, 2));
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, otherTenant, Ridden(elsewhere, 3));

        var response = await anonymous.GetAsync($"/api/participations?filter=eventId eq {eventId}");

        var ids = (await ApiSessions.ReadJsonAsync(response))
            .GetProperty("data")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetString());
        Assert.Equal([mine.Id.ToString()], ids);
    }

    [Fact]
    public async Task A_filter_after_the_Event_narrows_the_list_and_a_sort_and_a_page_work_as_they_do_for_every_list()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var participations = Enumerable.Range(1, 4).Select(x => Ridden(eventId, x)).ToList();
        foreach (var participation in participations)
        {
            await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        }

        var narrowed = await anonymous.GetAsync(
            $"/api/participations?filter=eventId eq {eventId} and combination/number gt 2&sort=-combination/number"
        );
        var page = await anonymous.GetAsync(
            $"/api/participations?filter=eventId eq {eventId}&sort=combination/number&page[size]=2&page[number]=2"
        );

        var numbers = (await ApiSessions.ReadJsonAsync(narrowed))
            .GetProperty("data")
            .EnumerateArray()
            .Select(x => x.GetProperty("attributes").GetProperty("combination").GetProperty("number").GetInt32());
        Assert.Equal([4, 3], numbers);
        var paged = await ApiSessions.ReadJsonAsync(page);
        Assert.Equal(
            [3, 4],
            paged
                .GetProperty("data")
                .EnumerateArray()
                .Select(x => x.GetProperty("attributes").GetProperty("combination").GetProperty("number").GetInt32())
        );
        Assert.False(
            paged.GetProperty("links").TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
        );
    }

    [Fact]
    public async Task A_filter_the_way_a_client_joins_its_filters_has_the_Event_in_parentheses_and_is_the_same_list()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var otherEvent = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        foreach (var participation in Enumerable.Range(1, 3).Select(x => Ridden(eventId, x)))
        {
            await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        }

        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, Ridden(otherEvent, 9));

        var response = await anonymous.GetAsync(
            $"/api/participations?filter=(eventId eq {eventId}) and (combination/number gt 1)&sort=combination/number"
        );
        var onlyTheEvent = await anonymous.GetAsync($"/api/participations?filter=(eventId eq {eventId})");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [2, 3],
            (await ApiSessions.ReadJsonAsync(response))
                .GetProperty("data")
                .EnumerateArray()
                .Select(x => x.GetProperty("attributes").GetProperty("combination").GetProperty("number").GetInt32())
        );
        Assert.Equal(3, (await ApiSessions.ReadJsonAsync(onlyTheEvent)).GetProperty("data").GetArrayLength());
    }

    [Theory]
    [InlineData("/api/participations")]
    [InlineData("/api/participations?filter=combination/number eq 1")]
    [InlineData("/api/participations?filter=eventId eq not-a-guid")]
    [InlineData(
        "/api/participations?filter=combination/number eq 1 and eventId eq 00000000-0000-0000-0000-000000000001"
    )]
    [InlineData(
        "/api/participations?filter=eventId eq 00000000-0000-0000-0000-000000000001 or combination/number eq 1"
    )]
    public async Task A_list_that_does_not_start_with_its_Event_is_refused(string path)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);

        var response = await anonymous.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("event-required", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task The_list_of_an_Event_that_is_not_there_is_empty()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);

        var response = await anonymous.GetAsync($"/api/participations?filter=eventId eq {Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ApiSessions.ReadJsonAsync(response)).GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task A_Participation_is_read_by_its_id_by_anybody_in_whatever_Tenant_it_is()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var participation = Ridden(eventId, 7);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation, version: 5);

        var response = await anonymous.GetAsync($"/api/participations/{participation.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal(participation.Id.ToString(), data.GetProperty("id").GetString());
        Assert.Equal(7, data.GetProperty("attributes").GetProperty("combination").GetProperty("number").GetInt32());
        Assert.Equal(5, data.GetProperty("meta").GetProperty("version").GetInt32());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-0000000000aa")]
    public async Task A_Participation_that_is_not_there_is_not_found(string id)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var anonymous = ApiClients.Of(api);

        var response = await anonymous.GetAsync($"/api/participations/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not-found", await ErrorCodeAsync(response));
    }

    /// <summary>A Participation of two Phases with an arrival recorded in the first.</summary>
    static Participation Ridden(Guid eventId, int number)
    {
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(
            eventId,
            number,
            Guid.NewGuid(),
            startTime: START
        );
        participation.Process(
            IntegrationPayloadFactory.ArriveSnapshot(number, START.AddHours(1)),
            TestId.Of(9),
            START.AddHours(2)
        );
        return participation;
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
