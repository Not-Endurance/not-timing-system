using System.Net;
using System.Text.Json;
using NoTiming.Api.Features.Reference;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// Who may read how the times of a Participation were recorded (#619, ADR-0005, ADR-0012): anybody reads the times each Phase
/// shows, and only Staff, which is every Official, every Operator and the Main Operator of the Event (and the Developer), read
/// its time events, with who recorded each and when. The Participation is the same resource for all of them and is read by
/// the caller. The people who recorded times are named to Staff only, and only the ones who recorded a time of the Event. The
/// documents are seeded straight into MongoDB.
/// </summary>
public sealed class ParticipationTimeEventsRoutesTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(2030, 5, 21, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddHours(1);

    readonly MongoFixture _mongo;

    public ParticipationTimeEventsRoutesTests(MongoFixture mongo)
    {
        ApiMongo.Configure();
        _mongo = mongo;
    }

    [Fact]
    public async Task Anybody_reads_the_times_of_a_Phase_and_nobody_but_Staff_reads_how_they_were_recorded()
    {
        await using var scene = await SceneAsync();

        var list = await scene.Anonymous.GetAsync($"/api/participations?filter=eventId eq {scene.EventId}");
        var one = await scene.Anonymous.GetAsync($"/api/participations/{scene.Participation.Id}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listed = (await ApiSessions.ReadJsonAsync(list)).GetProperty("data").EnumerateArray().Single();
        foreach (
            var attributes in new[]
            {
                listed.GetProperty("attributes"),
                (await ApiSessions.ReadJsonAsync(one)).GetProperty("data").GetProperty("attributes"),
            }
        )
        {
            var phase = attributes.GetProperty("phases")[0];
            Assert.Equal(0, phase.GetProperty("events").GetArrayLength());
            Assert.Equal(ARRIVE.AddMinutes(3), phase.GetProperty("arriveTime").GetDateTimeOffset());
            Assert.False(phase.TryGetProperty("presentTime", out _)); // a time the Phase does not show is not told
            Assert.False(phase.TryGetProperty("representTime", out _));
            Assert.Equal(0, attributes.GetProperty("phases")[1].GetProperty("events").GetArrayLength());
        }
    }

    [Fact]
    public async Task A_read_that_answers_by_the_caller_says_so_and_one_that_does_not_has_nothing_to_say()
    {
        await using var scene = await SceneAsync();

        var list = await scene.Anonymous.GetAsync($"/api/participations?filter=eventId eq {scene.EventId}");
        var one = await scene.Official.Page.GetAsync($"/api/participations/{scene.Participation.Id}");
        var rankings = await scene.Anonymous.GetAsync($"/api/rankings?filter=eventId eq {scene.EventId}");

        Assert.Contains("Cookie", list.Headers.Vary);
        Assert.Contains("Cookie", one.Headers.Vary);
        Assert.Empty(rankings.Headers.Vary);
    }

    [Fact]
    public async Task A_signed_in_account_that_is_not_Staff_reads_it_as_anybody_does()
    {
        await using var scene = await SceneAsync();

        foreach (var person in new[] { scene.Stranger, scene.OfficialOfAnotherEvent, scene.PendingOfficial })
        {
            var response = await person.Page.GetAsync($"/api/participations/{scene.Participation.Id}");

            var phase = (await ApiSessions.ReadJsonAsync(response))
                .GetProperty("data")
                .GetProperty("attributes")
                .GetProperty("phases")[0];
            Assert.Equal(0, phase.GetProperty("events").GetArrayLength());
            Assert.Equal(ARRIVE.AddMinutes(3), phase.GetProperty("arriveTime").GetDateTimeOffset());
        }
    }

    [Fact]
    public async Task An_Official_an_Operator_the_Main_Operator_and_the_Developer_read_the_time_events_with_who_recorded_each_and_when()
    {
        await using var scene = await SceneAsync();

        foreach (var person in new[] { scene.Official, scene.Operator, scene.MainOperator, scene.Developer })
        {
            var list = await person.Page.GetAsync($"/api/participations?filter=eventId eq {scene.EventId}");
            var one = await person.Page.GetAsync($"/api/participations/{scene.Participation.Id}");

            foreach (var response in new[] { list, one })
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var body = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
                var attributes = (
                    body.ValueKind == JsonValueKind.Array ? body.EnumerateArray().Single() : body
                ).GetProperty("attributes");
                var phase = attributes.GetProperty("phases")[0];
                var events = phase.GetProperty("events").EnumerateArray().ToList();
                Assert.Equal(
                    ["Arrived", "Arrived", "ArriveUpdated"],
                    events.Select(x => x.GetProperty("kind").GetString()).ToArray()
                );
                Assert.Equal(
                    ["Accepted", "RejectedDuplicateArrive", "Accepted"],
                    events.Select(x => x.GetProperty("outcome").GetString()).ToArray()
                );
                Assert.All(
                    events,
                    x => Assert.Equal(scene.Official.Id.ToString(), x.GetProperty("actorId").GetString())
                );
                Assert.All(events, x => Assert.Equal(JsonValueKind.String, x.GetProperty("recordedAt").ValueKind));
                Assert.All(events, x => Assert.Equal("Manual", x.GetProperty("method").GetString()));
                Assert.Equal(ARRIVE.AddMinutes(3), phase.GetProperty("arriveTime").GetDateTimeOffset());
            }
        }
    }

    [Fact]
    public async Task What_a_Main_Operator_writes_is_answered_with_the_Participation_as_Staff_read_it()
    {
        await using var scene = await SceneAsync();
        var response = await scene.MainOperator.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/participations/{scene.Participation.Id}",
            "participations",
            new { eliminated = (object?)null },
            id: scene.Participation.Id.ToString(),
            meta: new { version = 0 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var phase = (await ApiSessions.ReadJsonAsync(response))
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("phases")[0];
        Assert.Equal(3, phase.GetProperty("events").GetArrayLength());
    }

    [Fact]
    public async Task The_people_who_recorded_times_are_named_to_Staff_and_nobody_else_is()
    {
        await using var scene = await SceneAsync();
        var path = $"/api/time-event-actors?filter=eventId eq {scene.EventId}";

        foreach (var person in new[] { scene.Official, scene.Operator, scene.MainOperator, scene.Developer })
        {
            var response = await person.Page.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var data = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").EnumerateArray().ToList();
            var named = Assert.Single(data);
            Assert.Equal("time-event-actors", named.GetProperty("type").GetString());
            Assert.Equal(scene.Official.Id.ToString(), named.GetProperty("id").GetString());
            Assert.Equal("Olga Official", named.GetProperty("attributes").GetProperty("displayName").GetString());
        }
    }

    [Fact]
    public async Task A_person_who_may_not_read_the_time_events_is_not_told_who_recorded_them()
    {
        await using var scene = await SceneAsync();
        var path = $"/api/time-event-actors?filter=eventId eq {scene.EventId}";

        var anonymous = await scene.Anonymous.GetAsync(path);
        var stranger = await scene.Stranger.Page.GetAsync(path);
        var pending = await scene.PendingOfficial.Page.GetAsync(path);
        var other = await scene.OfficialOfAnotherEvent.Page.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, pending.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal("not-allowed", await ErrorCodeAsync(stranger));
    }

    [Theory]
    [InlineData("/api/time-event-actors")]
    [InlineData("/api/time-event-actors?filter=name eq 'x'")]
    [InlineData("/api/time-event-actors?filter=eventId eq not-a-guid")]
    [InlineData("/api/time-event-actors?filter=eventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301&sort=name")]
    public async Task A_list_of_the_people_that_does_not_name_its_Event_and_nothing_else_is_refused(string path)
    {
        await using var scene = await SceneAsync();

        var response = await scene.MainOperator.Page.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_people_of_an_Event_that_is_not_there_are_not_found()
    {
        await using var scene = await SceneAsync();

        var response = await scene.MainOperator.Page.GetAsync(
            "/api/time-event-actors?filter=eventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_Event_whose_times_were_recorded_by_nobody_has_nobody_to_name()
    {
        await using var scene = await SceneAsync();
        var quiet = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            scene.Tenant,
            scene.MainOperator.Id,
            DateTimeOffset.UtcNow
        );

        var response = await scene.MainOperator.Page.GetAsync($"/api/time-event-actors?filter=eventId eq {quiet}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ApiSessions.ReadJsonAsync(response)).GetProperty("data").EnumerateArray());
    }

    async Task<Scene> SceneAsync()
    {
        var api = new ApiFactory(_mongo.ConnectionString);
        var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var other = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Olga Official");
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );
        var operatorOf = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Operator",
            null,
            operatorOf.Email,
            operatorOf.Id
        );
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var stranger = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var pending = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Official",
            "Steward",
            pending.Email,
            null
        ); // not attached to it
        var ofAnother = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            other,
            "Official",
            "Steward",
            ofAnother.Email,
            ofAnother.Id
        );
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, Ridden(other, 2, stranger.Id)); // a time of another Event, recorded by somebody who is not named for this one
        var participation = Ridden(eventId, 1, official.Id);
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        return new Scene(
            api,
            client,
            tenant,
            eventId,
            mainOperator,
            official,
            operatorOf,
            developer,
            stranger,
            pending,
            ofAnother,
            participation
        );
    }

    /// <summary>A Participation of two Phases whose first has an arrival, a second one refused as a duplicate, and an update.</summary>
    static Participation Ridden(Guid eventId, int number, Guid official)
    {
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(
            eventId,
            number,
            Guid.NewGuid(),
            startTime: START
        );
        var arrived = participation.Process(
            IntegrationPayloadFactory.ArriveSnapshot(number, ARRIVE),
            official,
            START.AddHours(2)
        );
        participation.Process(
            IntegrationPayloadFactory.ArriveSnapshot(number, ARRIVE.AddMinutes(1)),
            official,
            START.AddHours(2).AddMinutes(1)
        );
        participation.UpdateSnapshot(
            arrived.Id,
            new Timestamp(ARRIVE.AddMinutes(3)),
            official,
            START.AddHours(2).AddMinutes(2)
        );
        return participation;
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    sealed class Scene : IAsyncDisposable
    {
        readonly ApiFactory _api;

        public Scene(
            ApiFactory api,
            HttpClient anonymous,
            string tenant,
            Guid eventId,
            Person mainOperator,
            Person official,
            Person operatorOf,
            Person developer,
            Person stranger,
            Person pendingOfficial,
            Person officialOfAnotherEvent,
            Participation participation
        )
        {
            _api = api;
            Anonymous = anonymous;
            Tenant = tenant;
            EventId = eventId;
            MainOperator = mainOperator;
            Official = official;
            Operator = operatorOf;
            Developer = developer;
            Stranger = stranger;
            PendingOfficial = pendingOfficial;
            OfficialOfAnotherEvent = officialOfAnotherEvent;
            Participation = participation;
        }

        public HttpClient Anonymous { get; }
        public string Tenant { get; }
        public Guid EventId { get; }
        public Person MainOperator { get; }
        public Person Official { get; }
        public Person Operator { get; }
        public Person Developer { get; }
        public Person Stranger { get; }
        public Person PendingOfficial { get; }
        public Person OfficialOfAnotherEvent { get; }
        public Participation Participation { get; }

        public async ValueTask DisposeAsync()
        {
            Anonymous.Dispose();
            await _api.DisposeAsync();
        }
    }
}
