using MongoDB.Bson;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using SystemTextJson = System.Text.Json.JsonSerializer;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0005, #620: an edit of the Phase form is stored in the Participation document as an event that lists the times it
/// changed, a time it cleared among them as a listed time with none, and loads whole; the JSON of the models round-trips
/// it through the serializer of the Api with the cleared time still listed.
/// </summary>
public sealed class PhaseUpdatedStorageTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(2026, 4, 28, 8, 0, 0, TimeSpan.Zero);
    static readonly Guid OFFICIAL = TestId.Of(9);
    static readonly Guid MAIN_OPERATOR = TestId.Of(10);

    readonly StoredDocuments _stored;

    public PhaseUpdatedStorageTests(MongoFixture mongo)
    {
        _stored = new StoredDocuments(mongo.ConnectionString);
    }

    [Fact]
    public async Task An_edit_of_the_Phase_form_is_stored_as_one_event_that_lists_its_times_and_loads_whole()
    {
        var eventId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var participation = Edited(eventId, id);
        await _stored.WriteParticipation(participation);

        var stored = await _stored.Read("event_participations", id);

        var phase = stored["Phases"].AsBsonArray[0].AsBsonDocument;
        var events = phase["Events"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
        Assert.Equal(["Arrived", "Presented", "PhaseUpdated"], events.Select(x => x["Kind"].AsString));
        var edit = events[2];
        Assert.Equal("Accepted", edit["Outcome"].AsString);
        Assert.Equal("Manual", edit["Method"].AsString);
        StoredDocuments.AssertStandardUuid(edit["_id"], participation.Phases[0].Events[2].Id);
        StoredDocuments.AssertStandardUuid(edit["ActorId"], MAIN_OPERATOR);
        Assert.Equal(BsonType.DateTime, edit["RecordedAt"].BsonType);
        Assert.DoesNotContain("Time", edit.Names); // it has no time of its own, it lists its times
        var listed = edit["Changes"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
        Assert.Equal(["Arrive", "Present"], listed.Select(x => x["Slot"].AsString));
        Assert.Equal(BsonType.DateTime, listed[0]["Time"].BsonType);
        Assert.True(!listed[1].Contains("Time") || listed[1]["Time"].IsBsonNull); // a time that was cleared has none

        var loaded = await _stored.LoadParticipation(id);

        AssertSameEdit(participation.Phases[0], loaded.Phases[0]);
        Assert.Equal(participation.Phases[0].ArriveTime, loaded.Phases[0].ArriveTime);
        Assert.Null(loaded.Phases[0].PresentTime);
    }

    [Fact]
    public void An_edit_of_the_Phase_form_round_trips_through_the_JSON_of_the_Api_with_the_cleared_time_listed()
    {
        var participation = Edited(Guid.NewGuid(), Guid.NewGuid());
        var model = ParticipationModel.MapFrom(participation);
        model.Version = 7;

        var received = SystemTextJson.Deserialize<ParticipationModel>(
            SystemTextJson.Serialize(model, JsonApiResults.Options),
            JsonApiResults.Options
        )!;

        AssertSameEdit(participation.Phases[0], received.MapToEntity().Phases[0]);
        Assert.Null(received.MapToEntity().Phases[0].PresentTime);
    }

    /// <summary>
    /// A Phase that an Official arrived at and presented, and that the Console Operator edited in the form: the Arrive time was
    /// moved and the Present time was cleared.
    /// </summary>
    static Participation Edited(Guid eventId, Guid id)
    {
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, id, startTime: START);
        participation.Process(
            IntegrationPayloadFactory.ArriveSnapshot(1, START.AddHours(1)),
            OFFICIAL,
            START.AddHours(5)
        );
        participation.Process(
            IntegrationPayloadFactory.PresentSnapshot(1, START.AddHours(1).AddMinutes(10)),
            OFFICIAL,
            START.AddHours(5).AddMinutes(1)
        );
        participation.Update(
            new Form(participation.Phases[0].Id, START, START.AddHours(1).AddMinutes(2), null),
            MAIN_OPERATOR,
            START.AddHours(5).AddMinutes(2)
        );
        return participation;
    }

    static void AssertSameEdit(Phase expected, Phase actual)
    {
        Assert.Equal(expected.Events.Count, actual.Events.Count);
        for (var index = 0; index < expected.Events.Count; index++)
        {
            var sent = expected.Events[index];
            var received = actual.Events[index];
            Assert.Equal(sent.GetType(), received.GetType());
            Assert.Equal(sent.Id, received.Id);
            Assert.Equal(
                sent.Times.Select(x => (x.Slot, x.Time?.ToDateTimeOffset())),
                received.Times.Select(x => (x.Slot, x.Time?.ToDateTimeOffset()))
            );
            Assert.Equal(sent.Outcome, received.Outcome);
            Assert.Equal(sent.Method, received.Method);
            Assert.Equal(sent.RecordedAt, received.RecordedAt);
            Assert.Equal(sent.ActorId, received.ActorId);
        }
    }

    sealed class Form : IPhaseState
    {
        public Form(Guid id, DateTimeOffset? startTime, DateTimeOffset? arriveTime, DateTimeOffset? presentTime)
        {
            Id = id;
            StartTime = startTime;
            ArriveTime = arriveTime;
            PresentTime = presentTime;
        }

        public Guid Id { get; }
        public DateTimeOffset? StartTime { get; }
        public DateTimeOffset? ArriveTime { get; }
        public DateTimeOffset? PresentTime { get; }
        public DateTimeOffset? RepresentTime => null;
    }
}
