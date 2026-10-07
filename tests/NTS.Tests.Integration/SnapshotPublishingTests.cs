using MongoDB.Bson;
using NoTiming.Api.Features.Reference;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The client of the Ui that sends the times it captured (#644, ADR-0013), over the real Api in this process and the
/// session cookie of an Official: a group goes in one request, in order, and each Snapshot of it is answered with what the
/// server recorded, or with why it was not, so that one that failed does not take the others with it. A group sent again is
/// answered with the first outcomes, because the device made the id of each Snapshot and the group makes the same ids every
/// time, and the Snapshots that were sent as Arrivals and are sent again as Presentations are other Snapshots. An Update
/// says what changed. What the client reads is what the Api serves, so the shapes meet here.
/// </summary>
public sealed class SnapshotPublishingTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(2030, 5, 21, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddMinutes(30);
    static readonly DateTimeOffset PRESENT = START.AddMinutes(40);

    readonly MongoFixture _mongo;

    public SnapshotPublishingTests(MongoFixture mongo)
    {
        ApiMongo.Configure();
        _mongo = mongo;
    }

    [Fact]
    public async Task A_group_is_sent_in_one_request_and_each_Snapshot_is_answered_with_what_the_server_recorded()
    {
        await using var scene = await SceneAsync(2);
        var group = GroupOf(SnapshotType.Arrive, ARRIVE, 1, 2);

        var receipts = await scene.Publisher.PublishSnapshotsAsync(scene.EventId, group);

        Assert.Equal(["POST /api/snapshots/actions/send-group"], scene.Requests.Asked);
        Assert.Equal(group.Entries.Select(group.IdOf), receipts.Select(x => x.Id));
        Assert.All(
            receipts,
            receipt =>
            {
                Assert.True(receipt.IsRecorded);
                Assert.True(receipt.IsAccepted);
                Assert.Equal(201, receipt.Status);
            }
        );
        var first = receipts[0].Snapshot!;
        Assert.Equal(scene.EventId, first.EventId);
        Assert.Equal(scene.ParticipationIds[0], first.ParticipationId);
        Assert.Equal(1, first.Number);
        Assert.Equal(TimeSlot.Arrive, first.Slot);
        Assert.Equal(ARRIVE.AddMinutes(1), first.Time);
        Assert.Equal(TimeEventOutcome.Accepted, first.Outcome);
        Assert.Equal("GATE1/40", first.Gate);
        Assert.NotNull(first.RecordedAt);
        foreach (var (id, number) in new[] { (scene.ParticipationIds[0], 1), (scene.ParticipationIds[1], 2) })
        {
            var recorded = Assert.Single(await EventsAsync(id));
            Assert.Equal(group.IdOf(group.Entries.Single(x => x.Number == number)), recorded["_id"].AsGuid);
            Assert.Equal(scene.Official.Id, recorded["ActorId"].AsGuid);
        }
    }

    [Fact]
    public async Task A_group_sent_again_is_answered_with_the_first_outcomes_and_records_nothing_twice()
    {
        await using var scene = await SceneAsync(2);
        var group = GroupOf(SnapshotType.Arrive, ARRIVE, 1, 2);
        var first = await scene.Publisher.PublishSnapshotsAsync(scene.EventId, group);

        var again = await scene.Publisher.PublishSnapshotsAsync(scene.EventId, group);

        Assert.All(again, receipt => Assert.Equal(200, receipt.Status));
        Assert.Equal(first.Select(x => x.Snapshot!.RecordedAt), again.Select(x => x.Snapshot!.RecordedAt));
        Assert.Equal(first.Select(x => x.Snapshot!.Outcome), again.Select(x => x.Snapshot!.Outcome));
        Assert.Single(await EventsAsync(scene.ParticipationIds[0]));
        Assert.Single(await EventsAsync(scene.ParticipationIds[1]));
    }

    [Fact]
    public async Task The_Snapshots_sent_as_Arrivals_and_sent_again_as_Presentations_are_recorded_as_Presentations()
    {
        await using var scene = await SceneAsync(1);
        var arrivals = GroupOf(SnapshotType.Arrive, ARRIVE, 1);
        await scene.Publisher.PublishSnapshotsAsync(scene.EventId, arrivals);
        var presentations = new SnapshotGroup(
            arrivals.Entries.Select(x => new Snapshot(x.Number, x.Name, null, new Timestamp(PRESENT))),
            SnapshotType.Present
        );

        var receipts = await scene.Publisher.PublishSnapshotsAsync(scene.EventId, presentations);

        var presented = Assert.Single(receipts);
        Assert.Equal(201, presented.Status);
        Assert.Equal(TimeSlot.Present, presented.Snapshot!.Slot);
        Assert.Equal(
            ["Arrived", "Presented"],
            (await EventsAsync(scene.ParticipationIds[0])).Select(x => x["Kind"].AsString)
        );
    }

    [Fact]
    public async Task A_Snapshot_the_server_did_not_record_is_a_receipt_with_its_code_and_the_others_of_the_group_are_recorded()
    {
        await using var scene = await SceneAsync(1);
        var group = GroupOf(SnapshotType.Arrive, ARRIVE, 1, 99);

        var receipts = await scene.Publisher.PublishSnapshotsAsync(scene.EventId, group);

        Assert.True(receipts[0].IsRecorded);
        var missing = receipts[1];
        Assert.False(missing.IsRecorded);
        Assert.Equal(404, missing.Status);
        Assert.Equal("participation-not-found", missing.ErrorCode);
        Assert.Contains("start number", missing.ErrorMessage);
        Assert.Equal(group.IdOf(group.Entries.Last()), missing.Id);
    }

    [Fact]
    public async Task A_Snapshot_the_server_recorded_as_rejected_is_a_receipt_with_its_reason()
    {
        await using var scene = await SceneAsync(1);
        await scene.Publisher.PublishSnapshotsAsync(scene.EventId, GroupOf(SnapshotType.Arrive, ARRIVE, 1));

        var again = await scene.Publisher.PublishSnapshotsAsync(scene.EventId, GroupOf(SnapshotType.Arrive, ARRIVE, 1));

        var rejected = Assert.Single(again);
        Assert.True(rejected.IsRecorded);
        Assert.False(rejected.IsAccepted);
        Assert.Equal(TimeEventOutcome.RejectedDuplicateArrive, rejected.Snapshot!.Outcome);
    }

    [Fact]
    public async Task A_group_the_Api_refuses_as_a_whole_is_a_receipt_that_says_why_for_each_of_its_Snapshots()
    {
        await using var scene = await SceneAsync(1);
        var anonymous = new SnapshotApiPublisher(JsonApiClients.Of(scene.Api, null, out _));
        var group = GroupOf(SnapshotType.Arrive, ARRIVE, 1);

        var receipts = await anonymous.PublishSnapshotsAsync(scene.EventId, group);

        var refused = Assert.Single(receipts);
        Assert.False(refused.IsRecorded);
        Assert.Equal(401, refused.Status);
        Assert.Equal("not-signed-in", refused.ErrorCode);
        Assert.Empty(await EventsAsync(scene.ParticipationIds[0]));
    }

    [Fact]
    public async Task A_Snapshot_of_an_Event_that_has_ended_is_a_receipt_with_the_code_of_the_refusal()
    {
        await using var scene = await SceneAsync(1);
        var historic = await EventSeed.HistoricAsync(
            _mongo.ConnectionString,
            scene.Tenant,
            scene.MainOperator.Id,
            DateTimeOffset.UtcNow
        );
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            scene.Tenant,
            historic,
            "Official",
            "Steward",
            scene.Official.Email,
            scene.Official.Id
        );

        var receipts = await scene.Publisher.PublishSnapshotsAsync(historic, GroupOf(SnapshotType.Arrive, ARRIVE, 1));

        var refused = Assert.Single(receipts);
        Assert.Equal(409, refused.Status);
        Assert.Equal("event-ended", refused.ErrorCode);
    }

    [Fact]
    public async Task More_Snapshots_than_a_request_takes_are_sent_in_parts_and_answered_in_the_order_of_the_group()
    {
        await using var scene = await SceneAsync(0);
        var participations = Enumerable
            .Range(1, 101)
            .Select(x =>
                IntegrationPayloadFactory.ActiveParticipation(scene.EventId, x, Guid.NewGuid(), startTime: START)
            )
            .ToList();
        await RegistrySeed
            .Collection(_mongo.ConnectionString, "event_participations")
            .InsertManyAsync(
                participations.Select(x =>
                {
                    var model = ParticipationModel.MapFrom(x);
                    model.TenantId = scene.Tenant;
                    return model.ToBsonDocument();
                })
            );
        var group = GroupOf(SnapshotType.Arrive, ARRIVE, Enumerable.Range(1, 101).ToArray());

        var receipts = await scene.Publisher.PublishSnapshotsAsync(scene.EventId, group);

        Assert.Equal(2, scene.Requests.Asked.Count);
        Assert.Equal(group.Entries.Select(group.IdOf), receipts.Select(x => x.Id));
        Assert.All(receipts, x => Assert.True(x.IsRecorded));
        Assert.Equal(Enumerable.Range(1, 101), receipts.Select(x => x.Snapshot!.Number));
    }

    [Fact]
    public async Task An_Update_is_a_receipt_that_says_what_changed_and_one_that_is_refused_says_why()
    {
        await using var scene = await SceneAsync(1);
        var group = GroupOf(SnapshotType.Arrive, ARRIVE, 1);
        var sent = (await scene.Publisher.PublishSnapshotsAsync(scene.EventId, group)).Single();
        var corrected = ARRIVE.AddMinutes(4);

        var update = await scene.Publisher.UpdateSnapshotAsync(sent.Id, corrected);
        var broken = await scene.Publisher.UpdateSnapshotAsync(sent.Id, START.AddHours(-1));
        var unknown = await scene.Publisher.UpdateSnapshotAsync(Guid.NewGuid(), corrected);

        Assert.Equal(200, update.Status);
        Assert.Equal(sent.Id, update.Id);
        Assert.True(update.IsAccepted);
        Assert.Equal(ARRIVE.AddMinutes(1), update.Snapshot!.PreviousTime);
        Assert.Equal(corrected, update.Snapshot.CurrentTime);
        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, broken.Snapshot!.Outcome);
        Assert.Equal(corrected, broken.Snapshot.CurrentTime);
        Assert.False(unknown.IsRecorded);
        Assert.Equal(404, unknown.Status);
        Assert.Equal("not-found", unknown.ErrorCode);
    }

    [Fact]
    public async Task The_Ui_sends_its_Snapshots_through_the_client_of_the_Api_and_not_over_the_live_connection()
    {
        await using var viewer = new ViewerDriver(
            new Uri("https://localhost"),
            new Uri("https://localhost"),
            null,
            "publisher"
        );

        var publisher = viewer.GetRequiredService<ISnapshotPublisher>();

        Assert.IsType<SnapshotApiPublisher>(publisher);
    }

    [Fact]
    public async Task A_group_the_Api_answers_with_fewer_results_than_it_has_Snapshots_is_not_taken_for_recorded()
    {
        await using var scene = await SceneAsync(2);
        scene.Requests.Answer = _ => Answered("""{ "meta": { "results": [] } }""");

        var receipts = await scene.Publisher.PublishSnapshotsAsync(
            scene.EventId,
            GroupOf(SnapshotType.Arrive, ARRIVE, 1, 2)
        );

        Assert.Equal(2, receipts.Count);
        Assert.All(
            receipts,
            receipt =>
            {
                Assert.False(receipt.IsRecorded);
                Assert.Equal(502, receipt.Status);
            }
        );
    }

    [Fact]
    public async Task An_answer_that_carries_no_Snapshot_is_not_taken_for_one_that_was_recorded()
    {
        await using var scene = await SceneAsync(1);
        scene.Requests.Answer = _ =>
            Answered("""{ "meta": { "results": [ { "status": 201, "data": { "type": "snapshots" } } ] } }""");

        var receipt = Assert.Single(
            await scene.Publisher.PublishSnapshotsAsync(scene.EventId, GroupOf(SnapshotType.Arrive, ARRIVE, 1))
        );

        Assert.False(receipt.IsRecorded);
        Assert.Contains("could not be read", receipt.ErrorMessage);
    }

    async Task<Scene> SceneAsync(int participations)
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
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );
        var ids = new List<Guid>();
        for (var number = 1; number <= participations; number++)
        {
            var participation = IntegrationPayloadFactory.ActiveParticipation(
                eventId,
                number,
                Guid.NewGuid(),
                startTime: START
            );
            await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
            ids.Add(participation.Id);
        }

        var publisher = new SnapshotApiPublisher(JsonApiClients.Of(api, official, out var requests));
        return new Scene(api, client, tenant, eventId, mainOperator, official, ids, publisher, requests);
    }

    async Task<List<BsonDocument>> EventsAsync(Guid participation)
    {
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation))!;
        return [.. stored["Phases"][0]["Events"].AsBsonArray.Select(x => x.AsBsonDocument)];
    }

    static HttpResponseMessage Answered(string body)
    {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/vnd.api+json"),
        };
    }

    /// <summary>The Snapshots of the numbers, each captured a minute after the one before it.</summary>
    static SnapshotGroup GroupOf(SnapshotType type, DateTimeOffset first, params int[] numbers)
    {
        return new SnapshotGroup(
            numbers.Select(
                (number, index) =>
                    new Snapshot(number, $"Rider {number}", null, new Timestamp(first.AddMinutes(index + 1)))
            ),
            type
        );
    }

    sealed class Scene : IAsyncDisposable
    {
        public Scene(
            ApiFactory api,
            HttpClient client,
            string tenant,
            Guid eventId,
            Person mainOperator,
            Person official,
            IReadOnlyList<Guid> participationIds,
            SnapshotApiPublisher publisher,
            JsonApiClients.Requests requests
        )
        {
            Api = api;
            Client = client;
            Tenant = tenant;
            EventId = eventId;
            MainOperator = mainOperator;
            Official = official;
            ParticipationIds = participationIds;
            Publisher = publisher;
            Requests = requests;
        }

        public ApiFactory Api { get; }
        public HttpClient Client { get; }
        public string Tenant { get; }
        public Guid EventId { get; }
        public Person MainOperator { get; }
        public Person Official { get; }

        /// <summary>The Participations of the numbers 1, 2 and so on.</summary>
        public IReadOnlyList<Guid> ParticipationIds { get; }

        /// <summary>The client of the Ui, signed in as the Official.</summary>
        public SnapshotApiPublisher Publisher { get; }

        public JsonApiClients.Requests Requests { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Api.DisposeAsync();
        }
    }
}
