using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NoTiming.Api.Features.Live;
using NoTiming.Api.Features.Reference;
using NTS.Contracts.Core.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The server records every time (#644, ADR-0013, ADR-0005): an Official or an Operator of a Live Event sends a Snapshot with
/// <c>POST</c>, carrying the id its device made for it, the Event, the start number, the kind and the time it captured, and the
/// answer is the time event the server recorded with its outcome, accepted or rejected with its reason. The same id again is
/// the first outcome and one event. A group is processed in order and continues past an entry that failed. An Update of a sent
/// Snapshot sets an absolute value and is answered the same way. The Participation is written against the version it was read
/// at, and a Snapshot that loses a race is read again and written again, so none is lost and none is refused for it. Every
/// write that was stored is announced once, and nobody needs to be connected for a time to be recorded. The documents are
/// seeded straight into MongoDB and read back from it.
/// </summary>
public sealed class SnapshotRoutesTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(2030, 5, 21, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddMinutes(30);
    static readonly DateTimeOffset PRESENT = START.AddMinutes(40);

    readonly MongoFixture _mongo;

    public SnapshotRoutesTests(MongoFixture mongo)
    {
        ApiMongo.Configure();
        _mongo = mongo;
    }

    [Fact]
    public async Task An_Official_posts_an_Arrive_and_the_server_records_it_as_an_accepted_time_event_and_answers_with_it()
    {
        await using var scene = await SceneAsync(new FakeTimeProvider(WholeSecond(DateTimeOffset.UtcNow)));
        var sent = Guid.NewGuid();

        var response = await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE, sent);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/snapshots/{sent}", response.Headers.Location?.OriginalString);
        var data = DataOf(await ApiSessions.ReadJsonAsync(response));
        Assert.Equal("snapshots", data.GetProperty("type").GetString());
        Assert.Equal(sent.ToString(), data.GetProperty("id").GetString());
        var attributes = data.GetProperty("attributes");
        Assert.Equal(scene.EventId.ToString(), attributes.GetProperty("eventId").GetString());
        Assert.Equal(scene.One.ToString(), attributes.GetProperty("participationId").GetString());
        Assert.Equal(1, attributes.GetProperty("number").GetInt32());
        Assert.Equal("Arrive", attributes.GetProperty("slot").GetString());
        Assert.Equal("Accepted", attributes.GetProperty("outcome").GetString());
        Assert.Equal(ARRIVE, attributes.GetProperty("time").GetDateTimeOffset());
        Assert.Equal(scene.Now, attributes.GetProperty("recordedAt").GetDateTimeOffset());
        Assert.Equal("GATE1/40", attributes.GetProperty("gate").GetString());
        Assert.Equal(
            (await StoredAsync(scene.One))["Phases"][0]["_id"].AsGuid.ToString(),
            attributes.GetProperty("phaseId").GetString()
        );
        var events = EventsOf(await StoredAsync(scene.One), 0);
        var recorded = Assert.Single(events);
        Assert.Equal(sent, recorded["_id"].AsGuid);
        Assert.Equal("Arrived", recorded["Kind"].AsString);
        Assert.Equal("Accepted", recorded["Outcome"].AsString);
        Assert.Equal("Manual", recorded["Method"].AsString);
        Assert.Equal(scene.Official.Id, recorded["ActorId"].AsGuid);
        Assert.Equal(scene.Now.UtcDateTime, recorded["RecordedAt"].ToUniversalTime());
        Assert.Equal(1, (await StoredAsync(scene.One))["Version"].AsInt32);
        Assert.Equal(ARRIVE, (await LoadedAsync(scene.One)).Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Equal([(scene.EventId, scene.One)], scene.Changes.Announced);
    }

    [Fact]
    public async Task A_Present_after_the_arrival_completes_the_Phase_and_each_write_is_announced_once()
    {
        await using var scene = await SceneAsync();

        var arrive = await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE);
        var present = await PostAsync(scene.Official, scene.EventId, 1, "Present", PRESENT);

        Assert.Equal(HttpStatusCode.Created, arrive.StatusCode);
        Assert.Equal(HttpStatusCode.Created, present.StatusCode);
        var attributes = DataOf(await ApiSessions.ReadJsonAsync(present)).GetProperty("attributes");
        Assert.Equal("Present", attributes.GetProperty("slot").GetString());
        Assert.Equal("Accepted", attributes.GetProperty("outcome").GetString());
        var stored = await StoredAsync(scene.One);
        Assert.Equal(2, stored["Version"].AsInt32);
        Assert.Equal(["Arrived", "Presented"], EventsOf(stored, 0).Select(x => x["Kind"].AsString).ToArray());
        Assert.True((await LoadedAsync(scene.One)).IsComplete());
        Assert.Equal([(scene.EventId, scene.One), (scene.EventId, scene.One)], scene.Changes.Announced);
    }

    [Fact]
    public async Task A_duplicate_arrive_is_recorded_as_a_rejected_event_with_its_reason_and_comes_back_in_the_response()
    {
        await using var scene = await SceneAsync();
        await PostAsync(scene.Official, scene.EventId, 2, "Arrive", ARRIVE);
        scene.Changes.Announced.Clear();
        var again = Guid.NewGuid();

        var response = await PostAsync(scene.Official, scene.EventId, 2, "Arrive", ARRIVE.AddMinutes(5), again);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode); // the event was recorded: the outcome is its own
        var attributes = DataOf(await ApiSessions.ReadJsonAsync(response)).GetProperty("attributes");
        Assert.Equal("RejectedDuplicateArrive", attributes.GetProperty("outcome").GetString());
        Assert.Equal("Arrive", attributes.GetProperty("slot").GetString());
        var events = EventsOf(await StoredAsync(scene.Two), 0);
        Assert.Equal(2, events.Count);
        Assert.Equal(again, events[1]["_id"].AsGuid);
        Assert.Equal("RejectedDuplicateArrive", events[1]["Outcome"].AsString);
        Assert.Equal(ARRIVE, (await LoadedAsync(scene.Two)).Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Equal([(scene.EventId, scene.Two)], scene.Changes.Announced); // a rejected event is a stored write too
    }

    [Fact]
    public async Task A_Snapshot_that_cannot_be_placed_is_recorded_as_a_rejected_invalid_time()
    {
        await using var scene = await SceneAsync();
        await PostAsync(scene.Official, scene.EventId, 2, "Arrive", ARRIVE);

        var response = await PostAsync(scene.Official, scene.EventId, 2, "Present", ARRIVE.AddMinutes(-5));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attributes = DataOf(await ApiSessions.ReadJsonAsync(response)).GetProperty("attributes");
        Assert.Equal("RejectedInvalidTime", attributes.GetProperty("outcome").GetString());
        Assert.Null((await LoadedAsync(scene.Two)).Phases[0].PresentTime);
        Assert.Equal(2, EventsOf(await StoredAsync(scene.Two), 0).Count);
    }

    [Fact]
    public async Task A_Snapshot_is_placed_by_its_own_time_in_the_next_Phase_once_that_has_started()
    {
        await using var scene = await SceneAsync();
        await PostAsync(scene.Official, scene.EventId, 2, "Arrive", ARRIVE);
        await PostAsync(scene.Official, scene.EventId, 2, "Present", PRESENT);
        var next = (await LoadedAsync(scene.Two)).Phases[1].StartTime!.ToDateTimeOffset();

        var response = await PostAsync(scene.Official, scene.EventId, 2, "Arrive", next.AddMinutes(20));

        var attributes = DataOf(await ApiSessions.ReadJsonAsync(response)).GetProperty("attributes");
        Assert.Equal("Accepted", attributes.GetProperty("outcome").GetString());
        Assert.Equal("GATE2/40", attributes.GetProperty("gate").GetString());
        Assert.Single(EventsOf(await StoredAsync(scene.Two), 1));
    }

    [Fact]
    public async Task The_user_that_recorded_a_time_is_the_one_signed_in_and_nothing_the_client_says_about_it_is_taken()
    {
        await using var scene = await SceneAsync();
        var other = Guid.NewGuid();

        var stamped = await scene.Official.Page.WriteAsync(
            HttpMethod.Post,
            "/api/snapshots",
            "snapshots",
            new
            {
                eventId = scene.EventId,
                number = 1,
                kind = "Arrive",
                time = ARRIVE,
                actorId = other,
                recordedAt = DateTimeOffset.UnixEpoch,
                method = "RFID",
            },
            id: Guid.NewGuid().ToString()
        );

        Assert.Equal(HttpStatusCode.BadRequest, stamped.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(stamped));
        Assert.Empty(EventsOf(await StoredAsync(scene.One), 0));
        Assert.Empty(scene.Changes.Announced);
    }

    [Fact]
    public async Task The_same_Snapshot_posted_again_is_the_first_outcome_and_one_event()
    {
        await using var scene = await SceneAsync();
        var sent = Guid.NewGuid();
        var first = await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE.AddTicks(1234), sent); // finer than the database keeps
        scene.Changes.Announced.Clear();

        var again = await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE.AddMinutes(9), sent);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var original = DataOf(await ApiSessions.ReadJsonAsync(first)).GetProperty("attributes");
        var repeated = DataOf(await ApiSessions.ReadJsonAsync(again)).GetProperty("attributes");
        Assert.Equal(original.GetProperty("outcome").GetString(), repeated.GetProperty("outcome").GetString());
        Assert.Equal(
            original.GetProperty("time").GetDateTimeOffset(),
            repeated.GetProperty("time").GetDateTimeOffset()
        );
        Assert.Equal(
            original.GetProperty("recordedAt").GetDateTimeOffset(),
            repeated.GetProperty("recordedAt").GetDateTimeOffset()
        );
        Assert.Single(EventsOf(await StoredAsync(scene.One), 0));
        Assert.Equal(1, (await StoredAsync(scene.One))["Version"].AsInt32);
        Assert.Empty(scene.Changes.Announced);
    }

    [Fact]
    public async Task A_rejected_Snapshot_posted_again_is_its_first_outcome_and_one_event_too()
    {
        await using var scene = await SceneAsync();
        await PostAsync(scene.Official, scene.EventId, 2, "Arrive", ARRIVE);
        var rejected = Guid.NewGuid();
        await PostAsync(scene.Official, scene.EventId, 2, "Arrive", ARRIVE.AddMinutes(5), rejected);

        var again = await PostAsync(scene.Official, scene.EventId, 2, "Arrive", ARRIVE.AddMinutes(5), rejected);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(
            "RejectedDuplicateArrive",
            DataOf(await ApiSessions.ReadJsonAsync(again)).GetProperty("attributes").GetProperty("outcome").GetString()
        );
        Assert.Equal(2, EventsOf(await StoredAsync(scene.Two), 0).Count);
    }

    [Fact]
    public async Task The_id_of_a_Snapshot_of_another_Participation_is_taken_and_records_nothing()
    {
        await using var scene = await SceneAsync();
        var sent = Guid.NewGuid();
        await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE, sent);
        scene.Changes.Announced.Clear();

        var response = await PostAsync(scene.Official, scene.EventId, 2, "Arrive", ARRIVE, sent);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("id-taken", await ErrorCodeAsync(response));
        Assert.Empty(EventsOf(await StoredAsync(scene.Two), 0));
        Assert.Empty(scene.Changes.Announced);
    }

    [Fact]
    public async Task A_start_number_the_Event_does_not_have_is_not_found_and_records_nothing()
    {
        await using var scene = await SceneAsync();

        var response = await PostAsync(scene.Official, scene.EventId, 99, "Arrive", ARRIVE);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("participation-not-found", await ErrorCodeAsync(response));
        Assert.Empty(scene.Changes.Announced);
    }

    [Fact]
    public async Task A_Snapshot_of_an_Event_that_is_not_there_is_not_found()
    {
        await using var scene = await SceneAsync();

        var response = await PostAsync(scene.Official, Guid.NewGuid(), 1, "Arrive", ARRIVE);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not-found", await ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData("id-missing", "id-required")]
    [InlineData("id-not-a-guid", "invalid-id")]
    [InlineData("kind-unknown", "invalid-snapshot")]
    [InlineData("kind-missing", "invalid-snapshot")]
    [InlineData("number-zero", "invalid-snapshot")]
    [InlineData("time-missing", "invalid-snapshot")]
    [InlineData("event-missing", "event-required")]
    public async Task A_Snapshot_that_is_not_made_as_one_is_refused_and_records_nothing(string flaw, string code)
    {
        await using var scene = await SceneAsync();
        var attributes = new Dictionary<string, object?>
        {
            ["eventId"] = scene.EventId,
            ["number"] = 1,
            ["kind"] = "Arrive",
            ["time"] = ARRIVE,
        };
        string? id = Guid.NewGuid().ToString();
        switch (flaw)
        {
            case "id-missing":
                id = null;
                break;
            case "id-not-a-guid":
                id = "seven";
                break;
            case "kind-unknown":
                attributes["kind"] = "Finish";
                break;
            case "kind-missing":
                attributes.Remove("kind");
                break;
            case "number-zero":
                attributes["number"] = 0;
                break;
            case "time-missing":
                attributes.Remove("time");
                break;
            case "event-missing":
                attributes.Remove("eventId");
                break;
        }

        var response = await scene.Official.Page.WriteAsync(
            HttpMethod.Post,
            "/api/snapshots",
            "snapshots",
            attributes,
            id: id
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await ErrorCodeAsync(response));
        Assert.Empty(EventsOf(await StoredAsync(scene.One), 0));
        Assert.Empty(scene.Changes.Announced);
    }

    [Fact]
    public async Task A_group_is_processed_in_order_and_continues_past_an_entry_that_failed_and_reports_each()
    {
        await using var scene = await SceneAsync();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();

        var response = await GroupAsync(
            scene.Official,
            Entry(ids[0], scene.EventId, 1, "Arrive", ARRIVE),
            Entry(ids[1], scene.EventId, 99, "Arrive", ARRIVE),
            Entry(ids[2], scene.EventId, 1, "Present", PRESENT),
            Entry(ids[3], scene.EventId, 1, "Arrive", ARRIVE.AddMinutes(1)),
            Entry(ids[4], scene.EventId, 1, "Bogus", ARRIVE)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = ResultsOf(await ApiSessions.ReadJsonAsync(response));
        Assert.Equal([201, 404, 201, 201, 400], results.Select(x => x.GetProperty("status").GetInt32()).ToArray());
        Assert.Equal("Accepted", OutcomeOf(results[0]));
        Assert.Equal("participation-not-found", ErrorCodeOf(results[1]));
        Assert.Equal("Accepted", OutcomeOf(results[2]));
        Assert.Equal("RejectedParticipationComplete", OutcomeOf(results[3])); // the ride was complete when it came
        Assert.Equal("invalid-snapshot", ErrorCodeOf(results[4]));
        Assert.Equal(
            [ids[0], ids[2], ids[3]],
            EventsOf(await StoredAsync(scene.One), 0).Select(x => x["_id"].AsGuid).ToArray()
        );
        Assert.Equal(3, (await StoredAsync(scene.One))["Version"].AsInt32);
        Assert.Equal(3, scene.Changes.Announced.Count);
    }

    [Fact]
    public async Task A_group_sent_again_reports_the_original_outcomes_and_records_nothing_more()
    {
        await using var scene = await SceneAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Task<HttpResponseMessage> Send()
        {
            return GroupAsync(
                scene.Official,
                Entry(first, scene.EventId, 1, "Arrive", ARRIVE),
                Entry(second, scene.EventId, 2, "Arrive", ARRIVE)
            );
        }

        await Send();
        scene.Changes.Announced.Clear();

        var again = await Send();

        var results = ResultsOf(await ApiSessions.ReadJsonAsync(again));
        Assert.Equal([200, 200], results.Select(x => x.GetProperty("status").GetInt32()).ToArray());
        Assert.Equal(["Accepted", "Accepted"], results.Select(OutcomeOf).ToArray());
        Assert.Single(EventsOf(await StoredAsync(scene.One), 0));
        Assert.Single(EventsOf(await StoredAsync(scene.Two), 0));
        Assert.Empty(scene.Changes.Announced);
    }

    [Fact]
    public async Task A_group_is_refused_when_it_is_not_a_list_or_has_too_many_entries_and_records_nothing()
    {
        await using var scene = await SceneAsync();
        var one = new
        {
            type = "snapshots",
            id = Guid.NewGuid(),
            attributes = new
            {
                eventId = scene.EventId,
                number = 1,
                kind = "Arrive",
                time = ARRIVE,
            },
        };

        var notAList = await SendJsonAsync(
            scene.Official,
            HttpMethod.Post,
            "/api/snapshots/actions/send-group",
            JsonSerializer.Serialize(new { data = one })
        );
        var tooMany = await GroupAsync(
            scene.Official,
            [.. Enumerable.Range(0, 101).Select(_ => Entry(Guid.NewGuid(), scene.EventId, 1, "Arrive", ARRIVE))]
        );

        Assert.Equal(HttpStatusCode.BadRequest, notAList.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(notAList));
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal("too-many-snapshots", await ErrorCodeAsync(tooMany));
        Assert.Empty(EventsOf(await StoredAsync(scene.One), 0));
    }

    [Fact]
    public async Task An_anonymous_caller_is_not_signed_in_and_records_nothing()
    {
        await using var scene = await SceneAsync();
        using var anonymous = ApiClients.Of(scene.Api);
        var page = new PageClient(anonymous);

        var single = await page.WriteAsync(
            HttpMethod.Post,
            "/api/snapshots",
            "snapshots",
            AttributesOf(scene.EventId, 1, "Arrive", ARRIVE),
            id: Guid.NewGuid().ToString()
        );
        var group = await SendJsonAsync(
            page,
            HttpMethod.Post,
            "/api/snapshots/actions/send-group",
            JsonSerializer.Serialize(new { data = Array.Empty<object>() })
        );
        var update = await page.WriteAsync(
            HttpMethod.Patch,
            $"/api/snapshots/{Guid.NewGuid()}",
            "snapshots",
            new { time = ARRIVE }
        );

        foreach (var refused in new[] { single, group, update })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        Assert.Empty(EventsOf(await StoredAsync(scene.One), 0));
    }

    [Theory]
    [InlineData(null, null, false, false, false)] // an account with nothing
    [InlineData(null, null, true, false, false)] // the Developer, who is not linked
    [InlineData(null, null, false, true, false)] // a Tenant Root, who is not linked
    [InlineData("Official", "TreatingVeterinaryCommissionMember", false, false, false)]
    [InlineData("Official", "VeterinaryServiceMember", false, false, false)]
    [InlineData("Official", "Steward", false, false, true)]
    [InlineData("Official", "ChiefSteward", false, false, true)]
    [InlineData("Official", "GroundJury", false, false, true)]
    [InlineData("Official", "GroundJuryPresident", false, false, true)]
    [InlineData("Operator", null, false, false, true)]
    public async Task Only_who_the_policy_lets_send_a_Snapshot_does_and_the_others_are_refused_and_record_nothing(
        string? kind,
        string? role,
        bool developer,
        bool tenantRoot,
        bool mayRecord
    )
    {
        await using var scene = await SceneAsync();
        var person = await SignedInAsync(
            scene.Api,
            scene.Client,
            _mongo.ConnectionString,
            scene.Tenant,
            tenantRoot ? TenantRootOf(scene.Tenant) : null,
            isDeveloper: developer
        );
        if (kind != null)
        {
            await EventSeed.GrantAsync(
                _mongo.ConnectionString,
                scene.Tenant,
                scene.EventId,
                kind,
                role,
                person.Email,
                person.Id
            );
        }

        var response = await PostAsync(person, scene.EventId, 1, "Arrive", ARRIVE);

        if (mayRecord)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Single(EventsOf(await StoredAsync(scene.One), 0));
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("not-allowed", await ErrorCodeAsync(response));
            Assert.Empty(EventsOf(await StoredAsync(scene.One), 0));
            Assert.Empty(scene.Changes.Announced);
        }
    }

    [Fact]
    public async Task The_Main_Operator_of_the_Event_sends_a_Snapshot_too()
    {
        await using var scene = await SceneAsync();

        var response = await PostAsync(scene.MainOperator, scene.EventId, 1, "Arrive", ARRIVE);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(scene.MainOperator.Id, EventsOf(await StoredAsync(scene.One), 0).Single()["ActorId"].AsGuid);
    }

    [Fact]
    public async Task An_Official_whose_access_was_removed_is_refused_on_the_next_POST_without_signing_in_again()
    {
        await using var scene = await SceneAsync();
        var first = await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE);
        await EventSeed
            .Grants(_mongo.ConnectionString)
            .DeleteOneAsync(new BsonDocument("_id", EventSeed.Binary(scene.OfficialGrant)));

        var next = await PostAsync(scene.Official, scene.EventId, 1, "Present", PRESENT);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
        Assert.Single(EventsOf(await StoredAsync(scene.One), 0));
    }

    [Fact]
    public async Task An_Event_that_has_not_started_or_has_ended_takes_no_Snapshot_and_writes_nothing()
    {
        await using var scene = await SceneAsync();
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, scene.Tenant, scene.MainOperator.Id);
        var historic = await EventSeed.HistoricAsync(
            _mongo.ConnectionString,
            scene.Tenant,
            scene.MainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var early = IntegrationPayloadFactory.ActiveParticipation(unstarted, 1, Guid.NewGuid(), startTime: START);
        var late = IntegrationPayloadFactory.ActiveParticipation(historic, 1, Guid.NewGuid(), startTime: START);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, scene.Tenant, early);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, scene.Tenant, late);
        foreach (var eventId in new[] { unstarted, historic })
        {
            await EventSeed.GrantAsync(
                _mongo.ConnectionString,
                scene.Tenant,
                eventId,
                "Official",
                "Steward",
                scene.Official.Email,
                scene.Official.Id
            );
        }

        var tooEarly = await PostAsync(scene.Official, unstarted, 1, "Arrive", ARRIVE);
        var tooLate = await PostAsync(scene.Official, historic, 1, "Arrive", ARRIVE);

        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        Assert.Equal("event-not-started", await ErrorCodeAsync(tooEarly));
        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(tooLate));
        Assert.Empty(EventsOf(await StoredAsync(early.Id), 0));
        Assert.Empty(EventsOf(await StoredAsync(late.Id), 0));
        Assert.Empty(scene.Changes.Announced);
    }

    [Fact]
    public async Task A_Snapshot_is_taken_until_the_last_second_of_the_Event_and_refused_from_the_instant_it_ends()
    {
        var now = WholeSecond(DateTimeOffset.UtcNow);
        var length = TimeSpan.FromHours(2);
        var time = new FakeTimeProvider(now);
        await using var scene = await SceneAsync(time);
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, scene.Tenant, scene.MainOperator.Id);
        await EventSeed.StartAsync(_mongo.ConnectionString, id, scene.Tenant, scene.MainOperator.Id, now + length);
        await EventSeed.ParticipationAsync(
            _mongo.ConnectionString,
            scene.Tenant,
            IntegrationPayloadFactory.ActiveParticipation(id, 1, Guid.NewGuid(), startTime: START)
        );
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            scene.Tenant,
            id,
            "Official",
            "Steward",
            scene.Official.Email,
            scene.Official.Id
        );

        time.Advance(length - TimeSpan.FromSeconds(1));
        var inTime = await PostAsync(scene.Official, id, 1, "Arrive", ARRIVE);
        time.Advance(TimeSpan.FromSeconds(1));
        var over = await PostAsync(scene.Official, id, 1, "Present", PRESENT);

        Assert.Equal(HttpStatusCode.Created, inTime.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(over));
    }

    [Fact]
    public async Task Of_many_Snapshots_posted_at_once_for_one_Participation_all_are_recorded_none_is_refused_and_the_version_counts_each()
    {
        await using var scene = await SceneAsync();
        var ids = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        var officials = new List<Person>();
        foreach (var _ in ids)
        {
            var official = await SignedInAsync(scene.Api, scene.Client, _mongo.ConnectionString, scene.Tenant);
            await EventSeed.GrantAsync(
                _mongo.ConnectionString,
                scene.Tenant,
                scene.EventId,
                "Official",
                "Steward",
                official.Email,
                official.Id
            );
            officials.Add(official);
        }

        var responses = await Task.WhenAll(
            ids.Select(
                (id, index) =>
                    index == 0
                        ? PostAsync(officials[index], scene.EventId, 1, "Arrive", ARRIVE, id)
                        : PostAsync(officials[index], scene.EventId, 1, "Present", PRESENT.AddMinutes(index), id)
            )
        );

        Assert.All(responses, x => Assert.Equal(HttpStatusCode.Created, x.StatusCode)); // none answers 409
        var stored = await StoredAsync(scene.One);
        Assert.Equal(8, stored["Version"].AsInt32);
        Assert.Equal(ids.Order().ToArray(), EventsOf(stored, 0).Select(x => x["_id"].AsGuid).Order().ToArray());
        Assert.Equal(8, scene.Changes.Announced.Count);
    }

    [Fact]
    public async Task An_Official_updates_the_arrival_they_sent_and_the_answer_says_what_changed()
    {
        await using var scene = await SceneAsync();
        var sent = Guid.NewGuid();
        await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE, sent);
        scene.Changes.Announced.Clear();
        var corrected = ARRIVE.AddMinutes(5);

        var response = await UpdateAsync(scene.Official, sent, corrected);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = DataOf(await ApiSessions.ReadJsonAsync(response));
        Assert.Equal(sent.ToString(), data.GetProperty("id").GetString());
        var attributes = data.GetProperty("attributes");
        Assert.Equal("Accepted", attributes.GetProperty("outcome").GetString());
        Assert.Equal("Arrive", attributes.GetProperty("slot").GetString());
        Assert.Equal(corrected, attributes.GetProperty("time").GetDateTimeOffset());
        Assert.Equal(ARRIVE, attributes.GetProperty("previousTime").GetDateTimeOffset());
        Assert.Equal(corrected, attributes.GetProperty("currentTime").GetDateTimeOffset());
        Assert.Equal("GATE1/40", attributes.GetProperty("gate").GetString());
        var stored = await StoredAsync(scene.One);
        var events = EventsOf(stored, 0);
        Assert.Equal(["Arrived", "ArriveUpdated"], events.Select(x => x["Kind"].AsString).ToArray());
        Assert.Equal(scene.Official.Id, events[1]["ActorId"].AsGuid);
        Assert.Equal(2, stored["Version"].AsInt32);
        Assert.Equal(corrected, (await LoadedAsync(scene.One)).Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Equal([(scene.EventId, scene.One)], scene.Changes.Announced);
    }

    [Fact]
    public async Task An_Update_of_a_Present_changes_the_presentation_and_makes_no_representation()
    {
        await using var scene = await SceneAsync();
        var sent = Guid.NewGuid();
        await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE);
        await PostAsync(scene.Official, scene.EventId, 1, "Present", PRESENT, sent);
        var corrected = PRESENT.AddMinutes(3);

        var response = await UpdateAsync(scene.Official, sent, corrected);

        var attributes = DataOf(await ApiSessions.ReadJsonAsync(response)).GetProperty("attributes");
        Assert.Equal("Present", attributes.GetProperty("slot").GetString());
        Assert.Equal(PRESENT, attributes.GetProperty("previousTime").GetDateTimeOffset());
        var loaded = await LoadedAsync(scene.One);
        Assert.Equal(corrected, loaded.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Null(loaded.Phases[0].RepresentTime);
        Assert.Equal(
            ["Arrived", "Presented", "PresentUpdated"],
            EventsOf(await StoredAsync(scene.One), 0).Select(x => x["Kind"].AsString).ToArray()
        );
    }

    [Fact]
    public async Task An_Update_that_breaks_the_order_is_recorded_as_rejected_says_why_and_leaves_the_time()
    {
        await using var scene = await SceneAsync();
        var sent = Guid.NewGuid();
        await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE, sent);
        scene.Changes.Announced.Clear();

        var response = await UpdateAsync(scene.Official, sent, START.AddMinutes(-30));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attributes = DataOf(await ApiSessions.ReadJsonAsync(response)).GetProperty("attributes");
        Assert.Equal("RejectedInvalidTime", attributes.GetProperty("outcome").GetString());
        Assert.Equal(ARRIVE, attributes.GetProperty("previousTime").GetDateTimeOffset());
        Assert.Equal(ARRIVE, attributes.GetProperty("currentTime").GetDateTimeOffset());
        Assert.Equal(ARRIVE, (await LoadedAsync(scene.One)).Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Equal(2, EventsOf(await StoredAsync(scene.One), 0).Count);
        Assert.Equal([(scene.EventId, scene.One)], scene.Changes.Announced);
    }

    [Fact]
    public async Task An_Update_replayed_is_harmless_because_it_sets_an_absolute_value()
    {
        await using var scene = await SceneAsync();
        var sent = Guid.NewGuid();
        await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE, sent);
        var corrected = ARRIVE.AddMinutes(5);

        await UpdateAsync(scene.Official, sent, corrected);
        var again = await UpdateAsync(scene.Official, sent, corrected);

        var attributes = DataOf(await ApiSessions.ReadJsonAsync(again)).GetProperty("attributes");
        Assert.Equal("Accepted", attributes.GetProperty("outcome").GetString());
        Assert.Equal(corrected, attributes.GetProperty("previousTime").GetDateTimeOffset());
        Assert.Equal(corrected, attributes.GetProperty("currentTime").GetDateTimeOffset());
        Assert.Equal(corrected, (await LoadedAsync(scene.One)).Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public async Task A_Present_after_a_representation_was_requested_is_the_Represent_time_and_an_Update_of_it_changes_the_representation()
    {
        await using var scene = await SceneAsync();
        var requested = IntegrationPayloadFactory.ActiveParticipation(
            scene.EventId,
            3,
            Guid.NewGuid(),
            startTime: START
        );
        requested.Process(
            IntegrationPayloadFactory.ArriveSnapshot(3, ARRIVE),
            scene.Official.Id,
            DateTimeOffset.UtcNow
        );
        requested.Process(
            IntegrationPayloadFactory.PresentSnapshot(3, PRESENT),
            scene.Official.Id,
            DateTimeOffset.UtcNow
        );
        requested.ToggleRepresentation(true, DateTimeOffset.UtcNow);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, scene.Tenant, requested);
        var represent = Guid.NewGuid();

        var sent = await PostAsync(scene.Official, scene.EventId, 3, "Present", PRESENT.AddMinutes(30), represent);
        var updated = await UpdateAsync(scene.Official, represent, PRESENT.AddMinutes(35));

        var attributes = DataOf(await ApiSessions.ReadJsonAsync(sent)).GetProperty("attributes");
        Assert.Equal("Represent", attributes.GetProperty("slot").GetString());
        Assert.Equal("Accepted", attributes.GetProperty("outcome").GetString());
        var change = DataOf(await ApiSessions.ReadJsonAsync(updated)).GetProperty("attributes");
        Assert.Equal("Represent", change.GetProperty("slot").GetString());
        Assert.Equal(PRESENT.AddMinutes(30), change.GetProperty("previousTime").GetDateTimeOffset());
        Assert.Equal(PRESENT.AddMinutes(35), change.GetProperty("currentTime").GetDateTimeOffset());
        var loaded = await LoadedAsync(requested.Id);
        Assert.Equal(PRESENT.AddMinutes(35), loaded.Phases[0].RepresentTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, loaded.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Equal(
            ["Arrived", "Presented", "Presented", "PresentUpdated"],
            EventsOf(await StoredAsync(requested.Id), 0).Select(x => x["Kind"].AsString).ToArray()
        );
    }

    [Fact]
    public async Task An_Update_with_a_time_finer_than_the_database_keeps_is_answered_with_the_time_that_is_stored()
    {
        await using var scene = await SceneAsync();
        var sent = Guid.NewGuid();
        await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE, sent);

        var response = await UpdateAsync(scene.Official, sent, ARRIVE.AddMinutes(5).AddTicks(1234)); // finer than the database keeps

        var attributes = DataOf(await ApiSessions.ReadJsonAsync(response)).GetProperty("attributes");
        var stored = (await LoadedAsync(scene.One)).Phases[0].ArriveTime!.ToDateTimeOffset();
        Assert.Equal(ARRIVE.AddMinutes(5), stored);
        Assert.Equal(stored, attributes.GetProperty("time").GetDateTimeOffset());
        Assert.Equal(stored, attributes.GetProperty("currentTime").GetDateTimeOffset());
        var recordedAt = attributes.GetProperty("recordedAt").GetDateTimeOffset();
        Assert.Equal(0, recordedAt.Ticks % TimeSpan.TicksPerMillisecond);
        var events = EventsOf(await StoredAsync(scene.One), 0);
        Assert.Equal(recordedAt.UtcDateTime, events[1]["RecordedAt"].ToUniversalTime());
    }

    [Fact]
    public async Task A_Participation_is_found_by_the_start_number_it_has_in_an_Event_and_by_the_time_event_it_holds_through_indexes()
    {
        await using var scene = await SceneAsync();

        var indexes = await (
            await RegistrySeed.Collection(_mongo.ConnectionString, "event_participations").Indexes.ListAsync()
        ).ToListAsync();

        var keys = indexes.Select(x => x["key"].AsBsonDocument.ToJson()).ToArray();
        Assert.Contains("""{ "EventId" : 1, "Combination.Number" : 1 }""", keys);
        Assert.Contains("""{ "Phases.Events._id" : 1 }""", keys);
    }

    [Fact]
    public async Task An_Update_names_a_Snapshot_that_is_there_and_is_taken_from_who_may_send_one_while_the_Event_is_Live()
    {
        await using var scene = await SceneAsync();
        var sent = Guid.NewGuid();
        await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE, sent);
        var outsider = await SignedInAsync(scene.Api, scene.Client, _mongo.ConnectionString, scene.Tenant);
        var historic = await EventSeed.HistoricAsync(
            _mongo.ConnectionString,
            scene.Tenant,
            scene.MainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var ended = IntegrationPayloadFactory.ActiveParticipation(historic, 1, Guid.NewGuid(), startTime: START);
        var endedSnapshot = Guid.NewGuid();
        ended.Process(
            new Snapshot(1, SnapshotType.Arrive, SnapshotMethod.Manual, new Timestamp(ARRIVE), endedSnapshot),
            scene.Official.Id,
            DateTimeOffset.UtcNow
        );
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, scene.Tenant, ended);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            scene.Tenant,
            historic,
            "Official",
            "Steward",
            scene.Official.Email,
            scene.Official.Id
        );

        var unknown = await UpdateAsync(scene.Official, Guid.NewGuid(), ARRIVE);
        var forbidden = await UpdateAsync(outsider, sent, ARRIVE.AddMinutes(1));
        var over = await UpdateAsync(scene.Official, endedSnapshot, ARRIVE.AddMinutes(1));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(over));
        Assert.Single(EventsOf(await StoredAsync(scene.One), 0));
        Assert.Single(EventsOf(await StoredAsync(ended.Id), 0));
    }

    [Fact]
    public async Task An_Update_is_refused_when_it_names_anything_but_the_time()
    {
        await using var scene = await SceneAsync();
        var sent = Guid.NewGuid();
        await PostAsync(scene.Official, scene.EventId, 1, "Arrive", ARRIVE, sent);

        var named = await scene.Official.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/snapshots/{sent}",
            "snapshots",
            new { time = ARRIVE.AddMinutes(1), number = 2 }
        );
        var without = await scene.Official.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/snapshots/{sent}",
            "snapshots",
            new { }
        );
        var mismatch = await scene.Official.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/snapshots/{sent}",
            "snapshots",
            new { time = ARRIVE.AddMinutes(1) },
            id: Guid.NewGuid().ToString()
        );

        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(named));
        Assert.Equal("invalid-snapshot", await ErrorCodeAsync(without));
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Equal("id-mismatch", await ErrorCodeAsync(mismatch));
        Assert.Single(EventsOf(await StoredAsync(scene.One), 0));
    }

    async Task<Scene> SceneAsync(FakeTimeProvider? time = null)
    {
        var changes = new RecordedChanges();
        var api = new ApiFactory(
            _mongo.ConnectionString,
            time: time,
            configureServices: services => services.AddSingleton<IParticipationChanges>(changes)
        );
        var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var clock = time?.GetUtcNow() ?? DateTimeOffset.UtcNow;
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, clock);
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var grant = await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );
        var one = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid(), startTime: START);
        var two = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 2, Guid.NewGuid(), startTime: START);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, one);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, two);
        return new Scene(api, client, changes, tenant, eventId, mainOperator, official, grant, one.Id, two.Id, clock);
    }

    async Task<BsonDocument> StoredAsync(Guid participation)
    {
        return (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation))!;
    }

    async Task<Participation> LoadedAsync(Guid participation)
    {
        return BsonSerializer.Deserialize<ParticipationModel>(await StoredAsync(participation)).MapToEntity();
    }

    static Task<HttpResponseMessage> PostAsync(
        Person person,
        Guid eventId,
        int number,
        string kind,
        DateTimeOffset time,
        Guid? id = null
    )
    {
        return person.Page.WriteAsync(
            HttpMethod.Post,
            "/api/snapshots",
            "snapshots",
            AttributesOf(eventId, number, kind, time),
            id: (id ?? Guid.NewGuid()).ToString()
        );
    }

    static Task<HttpResponseMessage> UpdateAsync(Person person, Guid snapshot, DateTimeOffset time)
    {
        return person.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/snapshots/{snapshot}",
            "snapshots",
            new { time },
            id: snapshot.ToString()
        );
    }

    static Task<HttpResponseMessage> GroupAsync(Person person, params object[] entries)
    {
        return SendJsonAsync(
            person.Page,
            HttpMethod.Post,
            "/api/snapshots/actions/send-group",
            JsonSerializer.Serialize(new { data = entries })
        );
    }

    static Task<HttpResponseMessage> SendJsonAsync(Person person, HttpMethod method, string path, string body)
    {
        return SendJsonAsync(person.Page, method, path, body);
    }

    static Task<HttpResponseMessage> SendJsonAsync(PageClient page, HttpMethod method, string path, string body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(body, Encoding.UTF8, ApiSessions.MEDIA_TYPE),
        };
        return page.SendAsync(request);
    }

    /// <summary>The stored end of an Event has the resolution of a millisecond: a test that stops the clock at its edge keeps to the second.</summary>
    static DateTimeOffset WholeSecond(DateTimeOffset value)
    {
        return new DateTimeOffset(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }

    static object Entry(Guid id, Guid eventId, int number, string kind, DateTimeOffset time)
    {
        return new
        {
            type = "snapshots",
            id,
            attributes = AttributesOf(eventId, number, kind, time),
        };
    }

    static object AttributesOf(Guid eventId, int number, string kind, DateTimeOffset time)
    {
        return new
        {
            eventId,
            number,
            kind,
            time,
        };
    }

    static JsonElement DataOf(JsonElement document)
    {
        return document.GetProperty("data");
    }

    static JsonElement[] ResultsOf(JsonElement document)
    {
        return [.. document.GetProperty("meta").GetProperty("results").EnumerateArray()];
    }

    static string? OutcomeOf(JsonElement result)
    {
        return result.GetProperty("data").GetProperty("attributes").GetProperty("outcome").GetString();
    }

    static string? ErrorCodeOf(JsonElement result)
    {
        return result.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    static List<BsonDocument> EventsOf(BsonDocument participation, int phase)
    {
        return [.. participation["Phases"][phase]["Events"].AsBsonArray.Select(x => x.AsBsonDocument)];
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    sealed class Scene : IAsyncDisposable
    {
        public Scene(
            ApiFactory api,
            HttpClient client,
            RecordedChanges changes,
            string tenant,
            Guid eventId,
            Person mainOperator,
            Person official,
            Guid officialGrant,
            Guid one,
            Guid two,
            DateTimeOffset now
        )
        {
            Api = api;
            Client = client;
            Changes = changes;
            Tenant = tenant;
            EventId = eventId;
            MainOperator = mainOperator;
            Official = official;
            OfficialGrant = officialGrant;
            One = one;
            Two = two;
            Now = now;
        }

        public ApiFactory Api { get; }
        public HttpClient Client { get; }
        public RecordedChanges Changes { get; }
        public string Tenant { get; }
        public Guid EventId { get; }
        public Person MainOperator { get; }

        /// <summary>A Steward of the Event.</summary>
        public Person Official { get; }

        public Guid OfficialGrant { get; }

        /// <summary>A Participation of one Phase, which is the last: number 1.</summary>
        public Guid One { get; }

        /// <summary>A Participation of two Phases: number 2.</summary>
        public Guid Two { get; }

        /// <summary>The instant of the clock the host was given, or of the real one when it was given none.</summary>
        public DateTimeOffset Now { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Api.DisposeAsync();
        }
    }
}
