using MongoDB.Bson;
using Newtonsoft.Json;
using Not.Serialization.JSON;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Core.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Nexus.HTTP.Mongo;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using Competition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;
using SystemTextJson = System.Text.Json.JsonSerializer;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0005: the time events of a Phase are stored in the Participation document as one discriminated type, and the times the
/// Phase shows are projected from them, so the flat Arrive, Present and Represent times are not stored. The documents go
/// through the Functions API and the raw BSON is read straight from MongoDB; the JSON of the models is round-tripped by both
/// serializers the platform uses.
/// </summary>
public sealed class TimeEventStorageTests : IClassFixture<NtsIntegrationFixture>
{
    static readonly DateTimeOffset START = new(2026, 4, 28, 8, 0, 0, TimeSpan.Zero);
    static readonly Guid OFFICIAL = TestId.Of(9);
    static readonly Guid STEWARD = TestId.Of(10);

    readonly NtsIntegrationFixture _fixture;
    readonly StoredDocuments _stored;

    public TimeEventStorageTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
        _stored = new StoredDocuments(fixture.MongoConnectionString);
    }

    [Fact]
    public async Task The_time_events_of_a_Phase_are_stored_as_one_discriminated_type_and_load_whole()
    {
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        var eventId = Guid.NewGuid();
        var id = Guid.NewGuid();
        await functionsApi.Create(IntegrationPayloadFactory.EventInformation(eventId));
        var participation = Ridden(eventId, id);
        await functionsApi.Create(participation);

        var stored = await _stored.Read(MongoConstants.PARTICIPATIONS_COLLECTION, id);

        var phase = stored["Phases"].AsBsonArray[0].AsBsonDocument;
        Assert.Empty(phase.Names.Intersect(["ArriveTime", "PresentTime", "RepresentTime", "IsReinspectionRequested"]));
        Assert.True(phase["IsRepresentRequested"].AsBoolean);
        var events = phase["Events"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
        Assert.Equal(
            ["Presented", "Arrived", "Arrived", "Presented", "Presented", "Presented"],
            events.Select(x => x["Kind"].AsString)
        );
        Assert.Equal(
            [
                "RejectedInvalidTime",
                "Accepted",
                "RejectedDuplicateArrive",
                "Accepted",
                "Accepted",
                "RejectedDuplicatePresent",
            ],
            events.Select(x => x["Outcome"].AsString)
        );
        Assert.Equal(
            [false, false, false, false, true, true],
            events.Select(x => x.Contains("IsRepresent") && x["IsRepresent"].AsBoolean)
        );
        for (var index = 0; index < events.Count; index++)
        {
            StoredDocuments.AssertStandardUuid(events[index]["_id"], participation.Phases[0].Events[index].Id);
        }
        StoredDocuments.AssertStandardUuid(events[0]["ActorId"], OFFICIAL);
        Assert.Equal(BsonType.DateTime, events[0]["RecordedAt"].BsonType);
        Assert.Equal(BsonType.DateTime, events[0]["Time"].BsonType);

        var loaded = await functionsApi.ReadParticipation(eventId, id);

        AssertSameEvents(participation.Phases[0], loaded.Phases[0]);
        Assert.Equal(participation.Phases[0].ArriveTime, loaded.Phases[0].ArriveTime);
        Assert.Equal(participation.Phases[0].PresentTime, loaded.Phases[0].PresentTime);
        Assert.Equal(participation.Phases[0].RepresentTime, loaded.Phases[0].RepresentTime);
        Assert.True(loaded.Phases[0].IsRepresentRequested);
    }

    [Fact]
    public async Task A_time_that_was_there_before_events_were_kept_is_stored_as_an_accepted_manual_event_with_no_actor()
    {
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        var eventId = Guid.NewGuid();
        var id = Guid.NewGuid();
        await functionsApi.Create(IntegrationPayloadFactory.EventInformation(eventId));
        var participation = WithTimes(eventId, id);
        await functionsApi.Create(participation);

        var stored = await _stored.Read(MongoConstants.PARTICIPATIONS_COLLECTION, id);

        var events = stored["Phases"]
            .AsBsonArray[0]
            .AsBsonDocument["Events"]
            .AsBsonArray.Select(x => x.AsBsonDocument)
            .ToList();
        Assert.Equal(2, events.Count);
        Assert.All(
            events,
            x =>
            {
                Assert.Equal("Accepted", x["Outcome"].AsString);
                Assert.Equal("Manual", x["Method"].AsString);
                Assert.DoesNotContain("ActorId", x.Names);
                Assert.DoesNotContain("RecordedAt", x.Names);
            }
        );

        var loaded = await functionsApi.ReadParticipation(eventId, id);

        AssertSameEvents(participation.Phases[0], loaded.Phases[0]);
        Assert.Equal(START.AddHours(1), loaded.Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Equal(START.AddHours(1).AddMinutes(10), loaded.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.All(loaded.Phases[0].Events, x => Assert.Null(x.ActorId));
    }

    [Fact]
    public async Task The_Version_of_a_Participation_is_stored_with_its_document()
    {
        var eventId = Guid.NewGuid();
        var id = Guid.NewGuid();
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        await functionsApi.Create(IntegrationPayloadFactory.EventInformation(eventId));
        var model = ParticipationModel.MapFrom(Ridden(eventId, id));
        model.Version = 3;

        await FunctionsRequests.Send(_fixture.FunctionsBaseUrl, HttpMethod.Post, "api/participations", model);

        var stored = await _stored.Read(MongoConstants.PARTICIPATIONS_COLLECTION, id);
        Assert.Equal(3, stored["Version"].AsInt32);
    }

    [Fact]
    public void The_time_events_round_trip_through_both_JSON_serializers_the_platform_uses()
    {
        var participation = Ridden(Guid.NewGuid(), Guid.NewGuid());
        var model = ParticipationModel.MapFrom(participation);
        model.Version = 7;

        var viaSystemText = SystemTextJson.Deserialize<ParticipationModel>(
            SystemTextJson.Serialize(model, JsonApiResults.Options),
            JsonApiResults.Options
        )!;
        var viaNewtonsoft = JsonConvert.DeserializeObject<ParticipationModel>(
            JsonConvert.SerializeObject(model, new NJsonSettings()),
            new NJsonSettings()
        )!;

        foreach (var received in new[] { viaSystemText, viaNewtonsoft })
        {
            AssertSameEvents(participation.Phases[0], received.MapToEntity().Phases[0]);
            Assert.Equal(7, received.Version);
            Assert.True(received.Phases[0].IsRepresentRequested);
        }
    }

    /// <summary>
    /// A Phase that was timed through every kind of outcome: an invalid presentation before the Start, an arrival and a second
    /// one that is a duplicate, a presentation, and, once a Representation is requested, a representation and a second
    /// presentation that is a duplicate.
    /// </summary>
    static Participation Ridden(Guid eventId, Guid id)
    {
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, id, startTime: START);
        Record(participation, SnapshotType.Present, SnapshotMethod.Manual, START.AddMinutes(-30), OFFICIAL, 0);
        Record(participation, SnapshotType.Arrive, SnapshotMethod.RFID, START.AddHours(1), OFFICIAL, 1);
        Record(participation, SnapshotType.Arrive, SnapshotMethod.Manual, START.AddHours(1).AddMinutes(5), STEWARD, 2);
        Record(
            participation,
            SnapshotType.Present,
            SnapshotMethod.Manual,
            START.AddHours(1).AddMinutes(10),
            OFFICIAL,
            3
        );
        participation.ToggleRepresentation(true, START.AddHours(1).AddMinutes(20));
        Record(
            participation,
            SnapshotType.Present,
            SnapshotMethod.Manual,
            START.AddHours(1).AddMinutes(35),
            OFFICIAL,
            4
        );
        Record(
            participation,
            SnapshotType.Present,
            SnapshotMethod.Manual,
            START.AddHours(1).AddMinutes(40),
            STEWARD,
            5
        );
        return participation;
    }

    static Participation WithTimes(Guid eventId, Guid id)
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Integration Rider", "Integration Rider", country, null, null, TestId.Of(101));
        var horse = new Horse("Integration Horse", "Integration Horse", null, TestId.Of(201));
        var combination = new Combination(1, athlete, horse, null, "20", null, null, TestId.Of(301));
        var phase = new Phase(
            gate: "GATE1/20",
            length: 20,
            maxRecovery: 40,
            rest: null,
            ruleset: CompetitionRuleset.FEI,
            isFinal: true,
            compulsoryThresholdSpan: null,
            startTime: new Timestamp(START),
            arriveTime: new Timestamp(START.AddHours(1)),
            presentTime: new Timestamp(START.AddHours(1).AddMinutes(10)),
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: TestId.Of(401)
        );

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("CEI 1*", CompetitionRuleset.FEI),
            combination,
            new PhaseCollection([phase]),
            notQualified: null,
            eventId,
            id
        );
    }

    static void Record(
        Participation participation,
        SnapshotType type,
        SnapshotMethod method,
        DateTimeOffset time,
        Guid actorId,
        int minutesAfterStart
    )
    {
        var snapshot = new Snapshot(1, type, method, new Timestamp(time));
        participation.Process(snapshot, actorId, START.AddHours(5).AddMinutes(minutesAfterStart));
    }

    static void AssertSameEvents(Phase expected, Phase actual)
    {
        Assert.Equal(expected.Events.Count, actual.Events.Count);
        for (var index = 0; index < expected.Events.Count; index++)
        {
            var sent = expected.Events[index];
            var received = actual.Events[index];
            Assert.Equal(sent.GetType(), received.GetType());
            Assert.Equal(sent.Id, received.Id);
            Assert.Equal(sent.Time.ToDateTimeOffset(), received.Time.ToDateTimeOffset());
            Assert.Equal(sent.Outcome, received.Outcome);
            Assert.Equal(sent.Method, received.Method);
            Assert.Equal(sent.RecordedAt, received.RecordedAt);
            Assert.Equal(sent.ActorId, received.ActorId);
            Assert.Equal((sent as Presented)?.IsRepresent, (received as Presented)?.IsRepresent);
        }
    }
}
