using MongoDB.Bson;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0006: what the domain derives from the recorded times is not stored. A written Participation holds the times and
/// nothing derived from them, and a stored one that still carries the derived values (from before) loads and shows what
/// the domain computes, because nothing reads them. The documents are written and loaded the way the application does
/// and the raw BSON is read straight from MongoDB.
/// </summary>
public sealed class DerivedValuesStorageTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(2026, 4, 28, 8, 0, 0, TimeSpan.Zero);

    readonly StoredDocuments _stored;

    public DerivedValuesStorageTests(MongoFixture mongo)
    {
        _stored = new StoredDocuments(mongo.ConnectionString);
    }

    [Fact]
    public async Task A_written_Participation_holds_none_of_the_values_the_domain_derives_and_no_Total()
    {
        var eventId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var participation = PresentedInTheFirstPhase(eventId, id);
        await _stored.WriteParticipation(participation);

        AssertNothingDerivedIsStored(await _stored.Read("event_participations", id));

        participation.Process(
            IntegrationPayloadFactory.ArriveSnapshot(1, START.AddHours(2).AddMinutes(30)),
            TestId.Of(9),
            START.AddHours(2).AddMinutes(30)
        );
        await _stored.ReplaceParticipation(participation);

        var updated = await _stored.Read("event_participations", id);
        AssertNothingDerivedIsStored(updated);
        Assert.Contains("Events", updated["Phases"].AsBsonArray[1].AsBsonDocument.Names); // the update did write
    }

    [Fact]
    public async Task A_stored_Participation_that_still_carries_the_derived_values_loads_and_shows_what_the_domain_computes()
    {
        var eventId = Guid.NewGuid();
        var id = Guid.NewGuid();
        await _stored.WriteParticipation(PresentedInTheFirstPhase(eventId, id));
        var stored = await _stored.Read("event_participations", id);
        LegacyDocuments.AddDerivedValues(stored);
        await _stored.Replace("event_participations", id, stored);

        var loaded = await _stored.LoadParticipation(id);

        // Worked out by hand: a 20 km Phase from 08:00, arriving 09:00 and presented 09:10, with a rest of 40 minutes.
        var phase = loaded.Phases[0];
        Assert.True(phase.IsComplete());
        Assert.Equal(TimeSpan.FromHours(1), phase.GetLoopInterval()!.ToTimeSpan());
        Assert.Equal(TimeSpan.FromMinutes(70), phase.GetPhaseInterval()!.ToTimeSpan());
        Assert.Equal(TimeSpan.FromMinutes(10), phase.GetRecoveryInterval()!.ToTimeSpan());
        Assert.Equal(20, phase.GetAverageLoopSpeed()!.ToDouble(), 9);
        Assert.Equal(20 / (70.0 / 60), phase.GetAveragePhaseSpeed()!.ToDouble(), 9);
        Assert.Equal(20 / (70.0 / 60), phase.GetAverageSpeed()!.ToDouble(), 9);
        Assert.Equal(START.AddMinutes(110), phase.GetOutTime()!.ToDateTimeOffset());
        Assert.Equal(START.AddMinutes(95), phase.GetRequiredInspectionTime()!.ToDateTimeOffset());
        var total = loaded.GetTotal()!;
        Assert.Equal(20 / (70.0 / 60), total.AverageSpeed.ToDouble(), 9);
        Assert.Equal(TimeSpan.FromHours(1), total.RideInterval.ToTimeSpan());
        Assert.Equal(TimeSpan.FromMinutes(10), total.RecoveryInterval.ToTimeSpan());
        Assert.Equal(TimeSpan.FromMinutes(70), total.Interval.ToTimeSpan());
    }

    static Participation PresentedInTheFirstPhase(Guid eventId, Guid id)
    {
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, id, startTime: START);
        participation.Process(
            IntegrationPayloadFactory.ArriveSnapshot(1, START.AddHours(1)),
            TestId.Of(9),
            START.AddHours(1)
        );
        participation.Process(
            IntegrationPayloadFactory.PresentSnapshot(1, START.AddHours(1).AddMinutes(10)),
            TestId.Of(9),
            START.AddHours(1).AddMinutes(10)
        );
        return participation;
    }

    static void AssertNothingDerivedIsStored(BsonDocument stored)
    {
        Assert.DoesNotContain("Total", stored.Names);
        var phases = stored["Phases"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
        Assert.Equal(2, phases.Count);
        Assert.All(phases, phase => Assert.Empty(phase.Names.Intersect(LegacyDocuments.DERIVED_PHASE_FIELDS)));
        Assert.Contains("Events", phases[0].Names); // what was recorded is stored, as time events
        Assert.Equal(2, phases[0]["Events"].AsBsonArray.Count);
    }
}
