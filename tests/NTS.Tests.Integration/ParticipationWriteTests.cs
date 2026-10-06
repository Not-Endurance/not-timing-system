using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NoTiming.Api.Features.Live;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// Writing the Participations of an Event (#604, ADR-0012, ADR-0013): the Main Operator's, while the Event is Live. A change
/// is made against the version the row was read at, which it increments: a change without one is refused, and a stale one
/// is 409 <c>participation-changed</c> and changes nothing. Every write that was stored is announced once to the viewers of
/// the Event, and one that was not is not. The documents are seeded straight into MongoDB and read back from it.
/// </summary>
public sealed class ParticipationWriteTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(2030, 5, 21, 8, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public ParticipationWriteTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Main_Operator_of_a_Live_Event_makes_a_Participation_and_the_viewers_are_told()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var made = Ridden(eventId, 1);

        var response = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/participations",
            "participations",
            AttributesOf(made),
            id: made.Id.ToString()
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/participations/{made.Id}", response.Headers.Location?.OriginalString);
        var data = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal(made.Id.ToString(), data.GetProperty("id").GetString());
        Assert.Equal(0, data.GetProperty("meta").GetProperty("version").GetInt32());
        Assert.Equal(1, data.GetProperty("attributes").GetProperty("combination").GetProperty("number").GetInt32());
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", made.Id))!;
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal(1, stored["Combination"]["Number"].AsInt32);
        Assert.False(stored.Contains("Version"));
        Assert.Equal([(eventId, made.Id)], changes.Announced);
    }

    [Fact]
    public async Task A_row_is_made_with_the_id_the_client_made_and_the_same_id_again_is_the_row_that_was_made()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var otherEvent = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var made = Ridden(eventId, 1);
        await Make(mainOperator, made);
        changes.Announced.Clear();

        var again = await Make(mainOperator, made);
        var elsewhere = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/participations",
            "participations",
            AttributesOf(Ridden(otherEvent, 2)),
            id: made.Id.ToString()
        );

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(
            made.Id.ToString(),
            (await ApiSessions.ReadJsonAsync(again)).GetProperty("data").GetProperty("id").GetString()
        );
        Assert.Equal(HttpStatusCode.Conflict, elsewhere.StatusCode); // that id is a row of another Event
        Assert.Equal("id-taken", await ErrorCodeAsync(elsewhere));
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task Only_the_Main_Operator_writes_a_row_of_a_Live_Event_and_nobody_else_not_even_the_Developer()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var existing = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, existing);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        using var anonymous = ApiClients.Of(api);
        var attempt = Ridden(eventId, 2);

        foreach (var person in new[] { root, member, developer })
        {
            var made = await person.Page.WriteAsync(
                HttpMethod.Post,
                "/api/participations",
                "participations",
                AttributesOf(attempt)
            );
            var changed = await Change(person, existing.Id, new { }, version: 0);
            var removed = await person.Page.DeleteAsync($"/api/participations/{existing.Id}");

            foreach (var refused in new[] { made, changed, removed })
            {
                Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
                Assert.Equal("not-main-operator", await ErrorCodeAsync(refused));
            }
        }

        var anonymousWrite = await new PageClient(anonymous).WriteAsync(
            HttpMethod.Post,
            "/api/participations",
            "participations",
            AttributesOf(attempt)
        );
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousWrite.StatusCode);
        Assert.NotNull(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", existing.Id));
        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", attempt.Id));
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task An_Event_that_has_not_started_takes_no_rows_and_one_that_has_ended_takes_no_more()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var historic = await EventSeed.HistoricAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var kept = Ridden(historic, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, kept);

        var early = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/participations",
            "participations",
            AttributesOf(Ridden(unstarted, 2))
        );
        var late = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/participations",
            "participations",
            AttributesOf(Ridden(historic, 3))
        );
        var changed = await Change(mainOperator, kept.Id, new { }, version: 0);
        var removed = await mainOperator.Page.DeleteAsync($"/api/participations/{kept.Id}");

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("event-not-started", await ErrorCodeAsync(early));
        foreach (var refused in new[] { late, changed, removed })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("event-ended", await ErrorCodeAsync(refused));
        }

        Assert.NotNull(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", kept.Id));
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task A_row_names_its_Event_which_has_to_be_there_and_a_row_the_domain_does_not_accept_is_refused()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var attributes = AttributesOf(Ridden(eventId, 1));
        var without = JsonApiAttributes.Without(attributes, "eventId");
        var elsewhere = AttributesOf(Ridden(Guid.NewGuid(), 2));
        var broken = JsonApiAttributes.Without(attributes, "combination");

        var unnamed = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/participations",
            "participations",
            without
        );
        var unknown = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/participations",
            "participations",
            elsewhere
        );
        var invalid = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/participations",
            "participations",
            broken
        );

        Assert.Equal(HttpStatusCode.BadRequest, unnamed.StatusCode);
        Assert.Equal("event-required", await ErrorCodeAsync(unnamed));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal("invalid-attribute", await ErrorCodeAsync(invalid));
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task A_change_is_made_against_the_version_it_was_read_at_which_it_increments_and_the_viewers_are_told()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation, version: 4);

        var response = await Change(
            mainOperator,
            participation.Id,
            new { eliminated = new { code = "WD" } },
            version: 4
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal(5, data.GetProperty("meta").GetProperty("version").GetInt32());
        Assert.Equal("WD", data.GetProperty("attributes").GetProperty("eliminated").GetProperty("code").GetString());
        var stored = (
            await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id)
        )!;
        Assert.Equal(5, stored["Version"].AsInt32);
        Assert.Equal("WD", stored["Eliminated"]["Code"].AsString);
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal(1, stored["Combination"]["Number"].AsInt32); // the members that were not named stay
        Assert.Equal([(eventId, participation.Id)], changes.Announced);
    }

    [Fact]
    public async Task A_row_that_was_never_written_since_the_versions_came_is_at_version_0_and_the_first_change_makes_it_1()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);

        var response = await Change(
            mainOperator,
            participation.Id,
            new { eliminated = new { code = "RET" } },
            version: 0
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            1,
            (await ApiSessions.ReadJsonAsync(response))
                .GetProperty("data")
                .GetProperty("meta")
                .GetProperty("version")
                .GetInt32()
        );
        Assert.Equal(
            1,
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id))![
                "Version"
            ].AsInt32
        );
    }

    [Fact]
    public async Task A_change_without_a_version_is_refused_and_a_stale_one_is_a_conflict_and_neither_changes_anything()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation, version: 4);
        var before = (
            await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id)
        )!;

        var without = await mainOperator.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/participations/{participation.Id}",
            "participations",
            new { eliminated = new { code = "WD" } }
        );
        var older = await Change(mainOperator, participation.Id, new { eliminated = new { code = "WD" } }, version: 3);
        var newer = await Change(mainOperator, participation.Id, new { eliminated = new { code = "WD" } }, version: 5);
        var notNumbers = new List<HttpResponseMessage>();
        foreach (var version in new object[] { "four", -1, 1.5, true, new { } })
        {
            notNumbers.Add(
                await mainOperator.Page.WriteAsync(
                    HttpMethod.Patch,
                    $"/api/participations/{participation.Id}",
                    "participations",
                    new { eliminated = new { code = "WD" } },
                    meta: new { version }
                )
            );
        }

        Assert.Equal(HttpStatusCode.BadRequest, without.StatusCode);
        Assert.Equal("version-required", await ErrorCodeAsync(without));
        foreach (var stale in new[] { older, newer })
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("participation-changed", await ErrorCodeAsync(stale));
        }

        foreach (var text in notNumbers)
        {
            Assert.Equal(HttpStatusCode.BadRequest, text.StatusCode);
            Assert.Equal("malformed-request", await ErrorCodeAsync(text));
        }

        Assert.Equal(
            before,
            await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id)
        );
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task Two_changes_made_at_the_same_version_are_not_both_taken_and_the_one_that_is_not_is_a_conflict()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation, version: 1);

        var both = await Task.WhenAll(
            Enumerable
                .Range(0, 6)
                .Select(_ =>
                    Change(mainOperator, participation.Id, new { eliminated = new { code = "RET" } }, version: 1)
                )
        );

        Assert.Single(both, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Equal(5, both.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(
            2,
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id))![
                "Version"
            ].AsInt32
        );
        Assert.Single(changes.Announced);
    }

    [Fact]
    public async Task A_change_that_names_nothing_changes_nothing_and_a_member_the_server_owns_or_keeps_is_refused()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation, version: 2);

        var nothing = await Change(mainOperator, participation.Id, new { }, version: 2);
        var staleNothing = await Change(mainOperator, participation.Id, new { }, version: 1);
        var refused = new List<HttpResponseMessage>();
        foreach (
            var attributes in new object[]
            {
                new { eventId = Guid.NewGuid() },
                new { tenantId = "country-xx" },
                new { version = 9 },
                new { isDeleted = true },
            }
        )
        {
            refused.Add(await Change(mainOperator, participation.Id, attributes, version: 2));
        }

        Assert.Equal(HttpStatusCode.Conflict, staleNothing.StatusCode); // a change that names nothing is made on a version too
        Assert.Equal("participation-changed", await ErrorCodeAsync(staleNothing));
        Assert.Equal(HttpStatusCode.OK, nothing.StatusCode);
        Assert.Equal(
            2,
            (await ApiSessions.ReadJsonAsync(nothing))
                .GetProperty("data")
                .GetProperty("meta")
                .GetProperty("version")
                .GetInt32()
        );
        Assert.All(
            refused,
            x =>
            {
                Assert.Equal(HttpStatusCode.BadRequest, x.StatusCode);
            }
        );
        var stored = (
            await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id)
        )!;
        Assert.Equal(2, stored["Version"].AsInt32);
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal(eventId, stored["EventId"].AsGuid);
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task A_change_the_domain_does_not_accept_is_refused_and_a_row_that_is_not_there_is_not_found()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation, version: 2);

        var invalid = await Change(
            mainOperator,
            participation.Id,
            new { eliminated = new { code = "DQ" } },
            version: 2
        );
        var missing = await Change(mainOperator, Guid.NewGuid(), new { }, version: 0);
        var removedMissing = await mainOperator.Page.DeleteAsync($"/api/participations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal("invalid-attribute", await ErrorCodeAsync(invalid));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removedMissing.StatusCode);
        Assert.Equal(
            2,
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id))![
                "Version"
            ].AsInt32
        );
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task The_Main_Operator_removes_a_Participation_and_the_viewers_are_told()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = Ridden(eventId, 1);
        var kept = Ridden(eventId, 2);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, kept);

        var response = await mainOperator.Page.DeleteAsync($"/api/participations/{participation.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation.Id));
        Assert.NotNull(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", kept.Id));
        Assert.Equal([(eventId, participation.Id)], changes.Announced);
    }

    [Fact]
    public async Task A_Participation_that_a_Ranking_counts_or_a_Handout_is_of_is_not_removed_until_they_let_go_of_it()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var counted = Ridden(eventId, 1);
        var handedOut = Ridden(eventId, 2);
        var free = Ridden(eventId, 3);
        foreach (var participation in new[] { counted, handedOut, free })
        {
            await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        }

        var ranking = IntegrationPayloadFactory.Ranking(eventId, [counted], Guid.NewGuid(), "CEI 1*");
        var handout = IntegrationPayloadFactory.Handout(handedOut, Guid.NewGuid());
        await EventSeed.RankingAsync(_mongo.ConnectionString, tenant, ranking);
        await EventSeed.HandoutAsync(_mongo.ConnectionString, tenant, handout);

        var ofARanking = await mainOperator.Page.DeleteAsync($"/api/participations/{counted.Id}");
        var ofAHandout = await mainOperator.Page.DeleteAsync($"/api/participations/{handedOut.Id}");
        var unnamed = await mainOperator.Page.DeleteAsync($"/api/participations/{free.Id}");
        var announcedWhileReferenced = changes.Announced.ToList();
        await mainOperator.Page.DeleteAsync($"/api/rankings/{ranking.Id}");
        await mainOperator.Page.DeleteAsync($"/api/handouts/{handout.Id}");
        var afterwards = new[]
        {
            await mainOperator.Page.DeleteAsync($"/api/participations/{counted.Id}"),
            await mainOperator.Page.DeleteAsync($"/api/participations/{handedOut.Id}"),
        };

        foreach (var refused in new[] { ofARanking, ofAHandout })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("participation-in-use", await ErrorCodeAsync(refused));
        }

        Assert.Equal(HttpStatusCode.NoContent, unnamed.StatusCode); // nothing names it
        Assert.Equal([(eventId, free.Id)], announcedWhileReferenced); // the refusals announced nothing
        Assert.All(afterwards, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));
        Assert.Equal(3, changes.Announced.Count);
        foreach (var removed in new[] { counted, handedOut, free })
        {
            Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", removed.Id));
        }
    }

    [Fact]
    public async Task A_row_that_is_in_another_Tenant_than_its_Event_is_not_written_through_the_Event()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var otherTenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var strayed = Ridden(eventId, 1);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, otherTenant, strayed, version: 1);
        var before = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", strayed.Id))!;

        var changed = await Change(mainOperator, strayed.Id, new { eliminated = new { code = "WD" } }, version: 1);
        var removed = await mainOperator.Page.DeleteAsync($"/api/participations/{strayed.Id}");

        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode); // what the Tenant of the Event holds is not that row
        Assert.Equal("participation-changed", await ErrorCodeAsync(changed));
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        Assert.Equal(
            before,
            await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", strayed.Id)
        );
        Assert.Empty(changes.Announced);
    }

    ApiFactory NewApi(IParticipationChanges changes)
    {
        return new ApiFactory(_mongo.ConnectionString, configureServices: services => services.AddSingleton(changes));
    }

    static Task<HttpResponseMessage> Make(Person person, Participation participation)
    {
        return person.Page.WriteAsync(
            HttpMethod.Post,
            "/api/participations",
            "participations",
            AttributesOf(participation),
            id: participation.Id.ToString()
        );
    }

    static Task<HttpResponseMessage> Change(Person person, Guid id, object attributes, int version)
    {
        return person.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/participations/{id}",
            "participations",
            attributes,
            meta: new { version }
        );
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

    /// <summary>What a client sends as the attributes of a Participation: its members, but for the ones the server owns.</summary>
    static Dictionary<string, JsonElement> AttributesOf(Participation participation)
    {
        return JsonApiAttributes.Of(ParticipationModel.MapFrom(participation));
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
