using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.EventData;
using NoTiming.Ui.Storage.Core.Repositories;
using NoTiming.Ui.Storage.REST;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// An Event that is no longer Live takes no write (#629, ADR-0007, ADR-0012): what the Main Operator may make, change and
/// remove while it runs is refused from the instant its last day ends, with 409 <c>event-ended</c>, and nothing is
/// written. The clock is the host's and is moved to the last second of the Event and to its end, so that the instant the
/// rule turns on is the instant tested, and a delete by id, which names no Event, is refused as well: the Event is found
/// from the row. Reads are never refused. The one write the host makes by itself to an Event that has ended is a named
/// rule, and no client reaches it.
/// </summary>
public sealed class EventEndedWritesTests : IClassFixture<MongoFixture>
{
    // A whole second, so that the end of the Event the database stores, which has no finer a resolution than a millisecond,
    // is the instant the clock is moved to and not an instant before it: the rule turns on at that very instant.
    static readonly DateTimeOffset NOW = WholeSecond(DateTimeOffset.UtcNow);
    static readonly TimeSpan LENGTH = TimeSpan.FromHours(2);

    readonly MongoFixture _mongo;

    public EventEndedWritesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Theory]
    [InlineData("participations")]
    [InlineData("rankings")]
    [InlineData("officials")]
    [InlineData("handouts")]
    public async Task Every_write_is_taken_until_the_last_day_of_the_Event_ends_and_refused_from_that_instant(
        string route
    )
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        using var anonymous = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await StartedAsync(tenant, mainOperator.Id);
        var changed = EventDataRow.Of(route, eventId);
        var removed = EventDataRow.Of(route, eventId);
        var made = EventDataRow.Of(route, eventId);
        await changed.SeedAsync(_mongo.ConnectionString, tenant);
        await removed.SeedAsync(_mongo.ConnectionString, tenant);
        var seeded = await StoredAsync(changed.Collection, eventId);

        time.Advance(LENGTH - TimeSpan.FromSeconds(1)); // the last second of the Event

        var created = await PostAsync(mainOperator, made);
        var updated = await PatchAsync(mainOperator, changed, version: 0);
        var deleted = await DeleteAsync(mainOperator, removed);
        var whileLive = await StoredAsync(changed.Collection, eventId);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(new[] { changed.Id, made.Id }.Order(), whileLive.Select(IdOf).Order());
        Assert.NotEqual(DocumentOf(seeded, changed.Id), DocumentOf(whileLive, changed.Id));

        time.Advance(TimeSpan.FromSeconds(1)); // the end of its last day

        await AssertRefusedAsync(mainOperator, route, eventId, changed, made, whileLive, version: 1);
        time.Advance(TimeSpan.FromDays(1)); // and it stays so
        await AssertRefusedAsync(mainOperator, route, eventId, changed, made, whileLive, version: 1);

        var list = await anonymous.GetAsync($"/api/{route}?filter=eventId eq {eventId}");
        var read = await anonymous.GetAsync($"/api/{route}/{changed.Id}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(2, (await ApiSessions.ReadJsonAsync(list)).GetProperty("data").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task Linking_and_unlinking_an_Operator_follow_the_same_clock()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var first = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var second = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await StartedAsync(tenant, mainOperator.Id);
        var existing = await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Operator",
            null,
            second.Email,
            second.Id
        );

        time.Advance(LENGTH - TimeSpan.FromSeconds(1));
        var linked = await LinkOperatorAsync(mainOperator, eventId, first.Email);
        var unlinked = await mainOperator.Page.DeleteAsync($"/api/event-grants/{existing}");
        var grant = Guid.Parse(
            (await ApiSessions.ReadJsonAsync(linked)).GetProperty("data").GetProperty("id").GetString()!
        );
        var whileLive = await GrantsOfAsync(eventId);
        time.Advance(TimeSpan.FromSeconds(1));
        var linkedAgain = await LinkOperatorAsync(mainOperator, eventId, second.Email);
        var unlinkedAgain = await mainOperator.Page.DeleteAsync($"/api/event-grants/{grant}");

        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unlinked.StatusCode);
        Assert.Equal([grant], whileLive.Select(IdOf));
        foreach (var refused in new[] { linkedAgain, unlinkedAgain })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("event-ended", await ErrorCodeAsync(refused));
        }

        Assert.Equal(whileLive, await GrantsOfAsync(eventId));
    }

    [Fact]
    public async Task The_Ui_is_told_the_code_of_a_refusal_event_ended_among_them_and_the_others_keep_theirs()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await StartedAsync(tenant, mainOperator.Id);
        var participation = EventDataRow.Of("participations", eventId);
        await participation.SeedAsync(_mongo.ConnectionString, tenant);
        var scope = new EventScopeFactory<Participation>(new SelectedEvent(eventId));
        var asMainOperator = new ParticipationEventScopedApiRepository(
            JsonApiClients.Of(api, mainOperator, out _),
            scope
        );
        var asMember = new ParticipationEventScopedApiRepository(JsonApiClients.Of(api, member, out _), scope);
        var read = (await asMainOperator.Read(participation.Id))!;
        var staleRead = (await asMainOperator.Read(participation.Id))!;
        var readByMember = (await asMember.Read(participation.Id))!;

        read.Withdraw();
        await asMainOperator.Update(read);
        var whileLive = asMainOperator.LastError;
        staleRead.Retire();
        await asMainOperator.Update(staleRead);
        var stale = asMainOperator.LastError;
        readByMember.Retire();
        await asMember.Update(readByMember);
        var notTheirs = asMember.LastError;

        time.Advance(LENGTH);
        var afterTheEnd = (await asMainOperator.Read(participation.Id))!; // reads are never refused
        afterTheEnd.Restore();
        await asMainOperator.Update(afterTheEnd);
        var updated = asMainOperator.LastError;
        await asMainOperator.Delete(participation.Id);
        var deleted = asMainOperator.LastError;
        await asMainOperator.Create(Created(eventId));
        var created = asMainOperator.LastError;

        Assert.Null(whileLive);
        Assert.Equal("participation-changed", stale?.Code);
        Assert.Equal("not-main-operator", notTheirs?.Code);
        foreach (var refused in new[] { updated, deleted, created })
        {
            Assert.Equal("event-ended", refused?.Code);
            Assert.Equal((int)HttpStatusCode.Conflict, refused?.Status);
        }

        var stored = (await StoredAsync(participation.Collection, eventId)).Single();
        Assert.Equal("WD", stored["Eliminated"]["Code"].AsString); // what was written while it was Live is what is there
    }

    [Fact]
    public async Task The_host_may_finalise_the_Rankings_of_an_Event_that_has_ended_by_a_named_rule_and_a_client_may_not_write_them()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await StartedAsync(tenant, mainOperator.Id);
        var ranking = EventDataRow.Of("rankings", eventId);
        await ranking.SeedAsync(_mongo.ConnectionString, tenant);
        var access = api.Services.GetRequiredService<EventDataAccess>();

        var whileLive = await access.OpenForHostAsync(HostOperation.RankingFinalisation, eventId, default);
        var clientWhileLive = await PatchAsync(mainOperator, ranking, version: 0);
        time.Advance(LENGTH);
        var atTheEnd = await access.OpenForHostAsync(HostOperation.RankingFinalisation, eventId, default);
        var clientAtTheEnd = await PatchAsync(mainOperator, ranking, version: 0);
        var notThere = await access.OpenForHostAsync(HostOperation.RankingFinalisation, Guid.NewGuid(), default);

        Assert.Null(whileLive); // not before the Event has ended
        Assert.Equal(HttpStatusCode.OK, clientWhileLive.StatusCode);
        Assert.Equal(eventId, atTheEnd?.Id);
        Assert.Equal(tenant, atTheEnd?.TenantId); // the host writes in the Tenant of the Event
        Assert.Equal(HttpStatusCode.Conflict, clientAtTheEnd.StatusCode); // the same Ranking, written by a client, is refused
        Assert.Equal("event-ended", await ErrorCodeAsync(clientAtTheEnd));
        Assert.Null(notThere);
    }

    [Fact]
    public async Task The_write_routes_of_what_an_Event_keeps_are_the_ones_whose_stage_is_tested_here()
    {
        await using var api = NewApi(out _);

        var families = new[] { "participations", "rankings", "officials", "handouts", "event-grants" };
        var actual = api
            .Services.GetServices<EndpointDataSource>()
            .SelectMany(x => x.Endpoints.OfType<RouteEndpoint>())
            .SelectMany(endpoint =>
                (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                    .Where(method => method is "POST" or "PUT" or "PATCH" or "DELETE")
                    .Select(method => (Method: method, Path: endpoint.RoutePattern.RawText!.TrimStart('/')))
            )
            .Where(route =>
                families.Any(family =>
                    route.Path == $"api/{family}" || route.Path.StartsWith($"api/{family}/", StringComparison.Ordinal)
                )
            )
            .Select(route => $"{route.Method} {route.Path}");

        // A write route that is added to what an Event keeps is a write that an Event that has ended has to refuse:
        // it is added here, and to the tests above, or it is not about what an Event keeps.
        Assert.Equal(
            new[]
            {
                "DELETE api/event-grants/{id}",
                "DELETE api/handouts/{id}",
                "DELETE api/officials/{id}",
                "DELETE api/participations/{id}",
                "DELETE api/rankings/{id}",
                "PATCH api/handouts/{id}",
                "PATCH api/officials/{id}",
                "PATCH api/participations/{id}",
                "PATCH api/rankings/{id}",
                "POST api/event-grants",
                "POST api/handouts",
                "POST api/officials",
                "POST api/participations",
                "POST api/rankings",
            }.Order(StringComparer.Ordinal),
            actual.Order(StringComparer.Ordinal)
        );
    }

    /// <summary>The three writes that are refused, and the proof that none of them wrote anything.</summary>
    async Task AssertRefusedAsync(
        Person mainOperator,
        string route,
        Guid eventId,
        EventDataRow changed,
        EventDataRow made,
        List<BsonDocument> expected,
        int version
    )
    {
        var refused = new[]
        {
            await PostAsync(mainOperator, EventDataRow.Of(route, eventId)),
            await PatchAsync(mainOperator, changed, version),
            await DeleteAsync(mainOperator, made), // by its id alone: the Event is found from the row
        };

        foreach (var response in refused)
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("event-ended", await ErrorCodeAsync(response));
        }

        Assert.Equal(expected, await StoredAsync(changed.Collection, eventId));
    }

    ApiFactory NewApi(out FakeTimeProvider time)
    {
        time = new FakeTimeProvider(NOW);
        return new ApiFactory(_mongo.ConnectionString, time: time);
    }

    /// <summary>An Event that has started and ends <see cref="LENGTH"/> after the clock of the host says it is.</summary>
    async Task<Guid> StartedAsync(string tenant, Guid mainOperator)
    {
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator);
        await EventSeed.StartAsync(_mongo.ConnectionString, id, tenant, mainOperator, NOW + LENGTH);
        return id;
    }

    async Task<List<BsonDocument>> StoredAsync(string collection, Guid eventId)
    {
        return await RegistrySeed
            .Collection(_mongo.ConnectionString, collection)
            .Find(new BsonDocument("EventId", RegistrySeed.Binary(eventId)))
            .Sort(new BsonDocument("_id", 1))
            .ToListAsync();
    }

    async Task<List<BsonDocument>> GrantsOfAsync(Guid eventId)
    {
        return await EventSeed
            .Grants(_mongo.ConnectionString)
            .Find(new BsonDocument("EventId", RegistrySeed.Binary(eventId)))
            .Sort(new BsonDocument("_id", 1))
            .ToListAsync();
    }

    static BsonDocument DocumentOf(List<BsonDocument> documents, Guid id)
    {
        return documents.Single(x => IdOf(x) == id);
    }

    static Guid IdOf(BsonDocument document)
    {
        return document["_id"].AsGuid;
    }

    static Participation Created(Guid eventId)
    {
        return IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 9, Guid.NewGuid());
    }

    static Task<HttpResponseMessage> PostAsync(Person person, EventDataRow row)
    {
        return person.Page.WriteAsync(
            HttpMethod.Post,
            $"/api/{row.Route}",
            row.Route,
            row.Attributes,
            id: row.Id.ToString()
        );
    }

    static Task<HttpResponseMessage> PatchAsync(Person person, EventDataRow row, int version)
    {
        return person.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/{row.Route}/{row.Id}",
            row.Route,
            row.Change,
            meta: row.MetaOf(version)
        );
    }

    static Task<HttpResponseMessage> DeleteAsync(Person person, EventDataRow row)
    {
        return person.Page.DeleteAsync($"/api/{row.Route}/{row.Id}");
    }

    static Task<HttpResponseMessage> LinkOperatorAsync(Person caller, Guid eventId, string email)
    {
        return caller.Page.WriteAsync(
            HttpMethod.Post,
            "/api/event-grants",
            "event-grants",
            new
            {
                eventId,
                kind = "operator",
                email,
            }
        );
    }

    static DateTimeOffset WholeSecond(DateTimeOffset instant)
    {
        return new DateTimeOffset(instant.Ticks - instant.Ticks % TimeSpan.TicksPerSecond, instant.Offset);
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
