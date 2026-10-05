using MongoDB.Bson;
using MongoDB.Driver;
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
using NTS.Tools.PhaseTimes;
using Competition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0005, <c>migrate-phase-times</c>: a Phase that stored its Arrive, Present and Represent times flat is turned into
/// one that stores them as accepted time events of the manual method, and the snapshot results go. The Participations are
/// seeded through the Functions API and then given the shape the code before #615 stored (flat times, the Representation
/// flag under its old name), so that what the application shows afterwards can be compared with the times they were made
/// with. The migration is tested through its command, over the documents as they are in MongoDB.
/// </summary>
public sealed class PhaseTimesMigrationTests : IClassFixture<NtsIntegrationFixture>, IAsyncLifetime
{
    const string PARTICIPATIONS = MongoConstants.PARTICIPATIONS_COLLECTION;
    const string RANKINGS = MongoConstants.RANKINGS_COLLECTION;
    const string HANDOUTS = MongoConstants.HANDOUTS_COLLECTION;
    const string SNAPSHOT_RESULTS = "event-snapshotResults";
    static readonly string[] COLLECTIONS = [PARTICIPATIONS, RANKINGS, HANDOUTS, SNAPSHOT_RESULTS];
    static readonly DateTimeOffset START = new(2026, 4, 28, 8, 0, 0, TimeSpan.Zero);

    readonly NtsIntegrationFixture _fixture;
    readonly StoredDocuments _stored;
    readonly IMongoDatabase _database;

    public PhaseTimesMigrationTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
        _stored = new StoredDocuments(fixture.MongoConnectionString);
        _database = new MongoClient(fixture.MongoConnectionString).GetDatabase(MongoConstants.NTS_DATABASE);
    }

    /// <summary>The migration reads every Participation, so each test starts from empty collections.</summary>
    public async Task InitializeAsync()
    {
        foreach (var collection in COLLECTIONS)
        {
            await _stored.Empty(collection);
        }
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_dry_run_changes_nothing_and_reports_the_counts_per_collection()
    {
        var seeded = await Seed();
        var before = await Everything();

        var report = await PhaseTimesMigration.Run(_database, apply: false);

        Assert.Equal(before, await Everything());
        Assert.False(report.Applied);
        Assert.False(report.Refused);
        Assert.Equal(4, report.Participations.Documents);
        Assert.Equal(2, report.Participations.Changed); // the Participation with times, and the final one
        Assert.Equal(7, report.Participations.Events); // five for the Participation with times, two for the final one
        Assert.Equal(2, report.SnapshotResults);
        Assert.False(report.SnapshotResultsDropped);
        Assert.Empty(report.Unreadable);
        Assert.Empty(report.Copies);
        Assert.NotNull(seeded);
    }

    [Fact]
    public async Task Apply_turns_each_flat_time_into_an_accepted_manual_event_and_the_projected_times_are_the_ones_before()
    {
        var seeded = await Seed();
        var documentsBefore = await ParticipationsByIdAsync();

        var report = await PhaseTimesMigration.Run(_database, apply: true);

        Assert.True(report.Applied);
        Assert.Equal(2, report.Participations.Changed);
        Assert.Equal(7, report.Participations.Events);
        var documentsAfter = await ParticipationsByIdAsync();
        AssertEventsOfTheTimedParticipation(documentsAfter[seeded.Timed.Id]);
        Assert.Equal(documentsBefore[seeded.Untimed.Id], documentsAfter[seeded.Untimed.Id]); // nothing to turn into events
        Assert.Equal(documentsBefore[seeded.AlreadyNew.Id], documentsAfter[seeded.AlreadyNew.Id]); // already in the new shape

        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        foreach (var original in seeded.All)
        {
            var loaded = await functionsApi.ReadParticipation(seeded.EventId, original.Id);
            AssertSameTimesAndFlags(original, loaded);
        }
    }

    [Fact]
    public async Task Apply_leaves_the_Start_and_the_flags_other_than_the_Representation_request_as_they_were()
    {
        var seeded = await Seed();
        var before = await ParticipationsByIdAsync();

        await PhaseTimesMigration.Run(_database, apply: true);

        var after = await ParticipationsByIdAsync();
        foreach (var (id, participation) in before)
        {
            var phasesBefore = participation["Phases"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
            var phasesAfter = after[id]["Phases"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
            Assert.Equal(phasesBefore.Count, phasesAfter.Count);
            for (var index = 0; index < phasesBefore.Count; index++)
            {
                foreach (
                    var name in new[]
                    {
                        "StartTime",
                        "IsRequiredInspectionRequested",
                        "IsRequiredInspectionCompulsory",
                        "Gate",
                        "Length",
                        "MaxRecovery",
                        "Rest",
                        "IsFinal",
                        "_id",
                    }
                )
                {
                    Assert.Equal(
                        phasesBefore[index].GetValue(name, BsonNull.Value),
                        phasesAfter[index].GetValue(name, BsonNull.Value)
                    );
                }
            }
        }

        Assert.NotNull(seeded);
    }

    [Fact]
    public async Task Applying_a_second_time_changes_nothing()
    {
        await Seed();
        await PhaseTimesMigration.Run(_database, apply: true);
        var afterTheFirst = await Everything();

        var report = await PhaseTimesMigration.Run(_database, apply: true);

        Assert.True(report.Applied);
        Assert.Equal(0, report.Participations.Changed);
        Assert.Equal(0, report.Participations.Events);
        Assert.Equal(0, report.SnapshotResults);
        Assert.False(report.SnapshotResultsDropped);
        Assert.Equal(afterTheFirst, await Everything());
    }

    [Fact]
    public async Task The_snapshot_results_are_dropped_on_apply_and_only_then()
    {
        await Seed();

        await PhaseTimesMigration.Run(_database, apply: false);
        Assert.Contains(SNAPSHOT_RESULTS, await CollectionNames());
        Assert.Equal(
            2,
            await _database
                .GetCollection<BsonDocument>(SNAPSHOT_RESULTS)
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)
        );

        var report = await PhaseTimesMigration.Run(_database, apply: true);

        Assert.True(report.SnapshotResultsDropped);
        Assert.DoesNotContain(SNAPSHOT_RESULTS, await CollectionNames());
    }

    [Fact]
    public async Task A_Phase_that_has_events_and_still_has_flat_times_keeps_its_events_and_loses_the_flat_ones()
    {
        var seeded = await Seed();
        var document = await _stored.Read(PARTICIPATIONS, seeded.AlreadyNew.Id);
        var phase = document["Phases"].AsBsonArray[0].AsBsonDocument;
        var events = phase["Events"].AsBsonArray.Select(x => x.DeepClone()).ToList();
        phase["ArriveTime"] = new BsonDateTime(START.AddHours(5).UtcDateTime);
        phase["IsReinspectionRequested"] = true;
        await _stored.Replace(PARTICIPATIONS, seeded.AlreadyNew.Id, document);

        await PhaseTimesMigration.Run(_database, apply: true);

        var migrated = (await _stored.Read(PARTICIPATIONS, seeded.AlreadyNew.Id))["Phases"]
            .AsBsonArray[0]
            .AsBsonDocument;
        Assert.Equal(events, migrated["Events"].AsBsonArray);
        Assert.DoesNotContain("ArriveTime", migrated.Names);
        Assert.DoesNotContain("IsReinspectionRequested", migrated.Names);
        Assert.True(migrated["IsRepresentRequested"].AsBoolean);
    }

    [Fact]
    public async Task A_Phase_with_an_empty_list_of_events_and_flat_times_gets_events_from_the_times()
    {
        var seeded = await Seed();
        var document = await _stored.Read(PARTICIPATIONS, seeded.Timed.Id);
        var first = document["Phases"].AsBsonArray[0].AsBsonDocument;
        first["Events"] = new BsonArray();
        await _stored.Replace(PARTICIPATIONS, seeded.Timed.Id, document);

        var report = await PhaseTimesMigration.Run(_database, apply: true);

        Assert.Equal(7, report.Participations.Events);
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        var loaded = await functionsApi.ReadParticipation(seeded.EventId, seeded.Timed.Id);
        AssertSameTimesAndFlags(seeded.Timed, loaded);
    }

    [Fact]
    public async Task A_flat_time_that_is_null_makes_no_event_and_is_removed()
    {
        var seeded = await Seed();
        var document = await _stored.Read(PARTICIPATIONS, seeded.Timed.Id);
        var final = document["Phases"].AsBsonArray[1].AsBsonDocument;
        final["ArriveTime"] = BsonNull.Value;
        final["RepresentTime"] = BsonNull.Value;
        await _stored.Replace(PARTICIPATIONS, seeded.Timed.Id, document);

        var report = await PhaseTimesMigration.Run(_database, apply: true);

        Assert.Empty(report.Unreadable);
        var migrated = (await _stored.Read(PARTICIPATIONS, seeded.Timed.Id))["Phases"].AsBsonArray[1].AsBsonDocument;
        Assert.Empty(migrated.Names.Intersect(["ArriveTime", "PresentTime", "RepresentTime"]));
        var only = Assert.Single(migrated["Events"].AsBsonArray).AsBsonDocument;
        Assert.Equal("Presented", only["Kind"].AsString);
        Assert.False(only.Contains("IsRepresent"));
    }

    [Fact]
    public async Task A_Phase_with_only_the_old_flag_gets_the_new_one_and_a_flag_that_is_false_is_only_removed()
    {
        var seeded = await Seed();
        var document = await _stored.Read(PARTICIPATIONS, seeded.Untimed.Id);
        var phases = document["Phases"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
        phases[0]["IsReinspectionRequested"] = true;
        phases[1]["IsReinspectionRequested"] = false;
        await _stored.Replace(PARTICIPATIONS, seeded.Untimed.Id, document);

        var report = await PhaseTimesMigration.Run(_database, apply: true);

        Assert.Equal(3, report.Participations.Changed); // the two with flat times, and this one
        Assert.Equal(7, report.Participations.Events);
        var migrated = (await _stored.Read(PARTICIPATIONS, seeded.Untimed.Id))["Phases"]
            .AsBsonArray.Select(x => x.AsBsonDocument)
            .ToList();
        Assert.True(migrated[0]["IsRepresentRequested"].AsBoolean);
        Assert.DoesNotContain("IsReinspectionRequested", migrated[0].Names);
        Assert.DoesNotContain("IsReinspectionRequested", migrated[1].Names);
        Assert.DoesNotContain("IsRepresentRequested", migrated[1].Names);
        Assert.DoesNotContain("Events", migrated[0].Names);
    }

    [Fact]
    public async Task A_time_that_is_not_a_date_is_reported_and_an_apply_refuses_and_writes_nothing()
    {
        var seeded = await Seed();
        var document = await _stored.Read(PARTICIPATIONS, seeded.Timed.Id);
        document["Phases"].AsBsonArray[0].AsBsonDocument["PresentTime"] = "09:10";
        await _stored.Replace(PARTICIPATIONS, seeded.Timed.Id, document);
        var before = await Everything();

        var dryRun = await PhaseTimesMigration.Run(_database, apply: false);
        var refused = await PhaseTimesMigration.Run(_database, apply: true);

        var unreadable = Assert.Single(dryRun.Unreadable);
        Assert.Equal("Phases[0].PresentTime", unreadable.Where);
        Assert.Equal(BsonType.String, unreadable.Type);
        Assert.Equal(1, dryRun.Participations.Changed); // the final one only: the Participation with the bad time waits
        Assert.Equal(LegacyDocuments.Uuid(seeded.Timed.Id), unreadable.DocumentId);
        Assert.False(dryRun.Refused);
        Assert.True(refused.Refused);
        Assert.False(refused.Applied);
        Assert.Equal(before, await Everything());
        Assert.Contains(SNAPSHOT_RESULTS, await CollectionNames());
    }

    [Fact]
    public async Task A_copy_of_a_Participation_that_is_still_in_a_Ranking_or_a_Handout_is_reported_and_an_apply_refuses()
    {
        var seeded = await Seed();
        var copy = await _stored.Read(PARTICIPATIONS, seeded.Timed.Id);
        await _stored.Insert(
            RANKINGS,
            new BsonDocument
            {
                ["_id"] = LegacyDocuments.Uuid(Guid.NewGuid()),
                ["EventId"] = LegacyDocuments.Uuid(seeded.EventId),
                ["Entries"] = new BsonArray
                {
                    new BsonDocument { ["ParticipationId"] = LegacyDocuments.Uuid(seeded.Untimed.Id) },
                    new BsonDocument { ["Participation"] = copy },
                },
            }
        );
        await _stored.Insert(
            HANDOUTS,
            new BsonDocument
            {
                ["_id"] = LegacyDocuments.Uuid(Guid.NewGuid()),
                ["EventId"] = LegacyDocuments.Uuid(seeded.EventId),
                ["Participation"] = copy,
            }
        );
        var before = await Everything();

        var dryRun = await PhaseTimesMigration.Run(_database, apply: false);
        var refused = await PhaseTimesMigration.Run(_database, apply: true);

        Assert.Equal([HANDOUTS, RANKINGS], dryRun.Copies.Select(x => x.Collection).Order().ToArray());
        Assert.False(dryRun.Refused);
        Assert.True(refused.Refused);
        Assert.False(refused.Applied);
        Assert.Equal(before, await Everything());
    }

    [Fact]
    public async Task A_Ranking_entry_or_a_Handout_that_holds_no_copy_does_not_stop_the_apply()
    {
        var seeded = await Seed();
        await _stored.Insert(
            RANKINGS,
            new BsonDocument
            {
                ["_id"] = LegacyDocuments.Uuid(Guid.NewGuid()),
                ["EventId"] = LegacyDocuments.Uuid(seeded.EventId),
                ["Entries"] = new BsonArray { new BsonDocument { ["Participation"] = BsonNull.Value } },
            }
        );
        await _stored.Insert(
            HANDOUTS,
            new BsonDocument
            {
                ["_id"] = LegacyDocuments.Uuid(Guid.NewGuid()),
                ["EventId"] = LegacyDocuments.Uuid(seeded.EventId),
                ["Participation"] = BsonNull.Value,
            }
        );

        var dryRun = await PhaseTimesMigration.Run(_database, apply: false);
        var applied = await PhaseTimesMigration.Run(_database, apply: true);

        Assert.Empty(dryRun.Copies);
        Assert.False(applied.Refused);
        Assert.True(applied.Applied);
    }

    [Fact]
    public async Task The_command_is_a_dry_run_unless_it_is_given_apply_and_prints_the_counts()
    {
        await Seed();
        var before = await Everything();

        var dryRun = await Command("--connection-string", _fixture.MongoConnectionString);

        Assert.Equal(0, dryRun.ExitCode);
        Assert.Contains("Dry-run phase-times migration.", dryRun.Output);
        Assert.Contains("event_participations: 4 documents read, 2 to change (7 events)", dryRun.Output);
        Assert.Contains("event-snapshotResults: 2 documents, to drop", dryRun.Output);
        Assert.Contains("Nothing was changed. Run again with --apply to persist.", dryRun.Output);
        Assert.Equal(before, await Everything());

        var applied = await Command("--connection-string", _fixture.MongoConnectionString, "--apply");

        Assert.Equal(0, applied.ExitCode);
        Assert.Contains("event_participations: 4 documents read, 2 changed (7 events)", applied.Output);
        Assert.Contains("Done.", applied.Output);
        Assert.DoesNotContain(_fixture.MongoConnectionString, applied.Output);
        Assert.DoesNotContain(SNAPSHOT_RESULTS, await CollectionNames());
    }

    [Fact]
    public async Task The_command_exits_with_1_when_it_is_asked_wrongly_or_refuses()
    {
        var seeded = await Seed();
        var document = await _stored.Read(PARTICIPATIONS, seeded.Timed.Id);
        document["Phases"].AsBsonArray[1].AsBsonDocument["ArriveTime"] = "09:00";
        await _stored.Replace(PARTICIPATIONS, seeded.Timed.Id, document);

        var withoutConnection = await Command("--apply");
        var unknown = await Command("--connection-string", _fixture.MongoConnectionString, "--bogus");
        var refused = await Command("--connection-string", _fixture.MongoConnectionString, "--apply");

        Assert.Equal(1, withoutConnection.ExitCode);
        Assert.Contains("--connection-string is required.", withoutConnection.Error);
        Assert.Equal(1, unknown.ExitCode);
        Assert.Contains("Unknown or incomplete option '--bogus'.", unknown.Error);
        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("Refusing to apply", refused.Output);
        Assert.Contains("Phases[1].ArriveTime: String", refused.Output);
    }

    static void AssertEventsOfTheTimedParticipation(BsonDocument document)
    {
        var phases = document["Phases"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
        var first = phases[0];
        Assert.Empty(first.Names.Intersect(["ArriveTime", "PresentTime", "RepresentTime", "IsReinspectionRequested"]));
        Assert.True(first["IsRepresentRequested"].AsBoolean);
        var events = first["Events"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
        Assert.Equal(["Arrived", "Presented", "Presented"], events.Select(x => x["Kind"].AsString));
        Assert.Equal([false, false, true], events.Select(x => x.Contains("IsRepresent") && x["IsRepresent"].AsBoolean));
        Assert.All(
            events,
            x =>
            {
                Assert.Equal("Accepted", x["Outcome"].AsString);
                Assert.Equal("Manual", x["Method"].AsString);
                Assert.DoesNotContain("RecordedAt", x.Names);
                Assert.DoesNotContain("ActorId", x.Names);
                Assert.Equal(BsonType.DateTime, x["Time"].BsonType);
                StoredDocuments.AssertStandardUuid(
                    x["_id"],
                    x["_id"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard)
                );
            }
        );
        Assert.Equal(
            [START.AddHours(1), START.AddHours(1).AddMinutes(10), START.AddHours(1).AddMinutes(30)],
            events.Select(x => new DateTimeOffset(x["Time"].ToUniversalTime()))
        );
        Assert.Equal(3, events.Select(x => x["_id"]).Distinct().Count());
        Assert.Equal(2, phases[1]["Events"].AsBsonArray.Count);
    }

    static void AssertSameTimesAndFlags(Participation original, Participation loaded)
    {
        Assert.Equal(original.Phases.Count, loaded.Phases.Count);
        for (var index = 0; index < original.Phases.Count; index++)
        {
            var expected = original.Phases[index];
            var actual = loaded.Phases[index];
            Assert.Equal(expected.ArriveTime, actual.ArriveTime);
            Assert.Equal(expected.PresentTime, actual.PresentTime);
            Assert.Equal(expected.RepresentTime, actual.RepresentTime);
            Assert.Equal(expected.StartTime, actual.StartTime);
            Assert.Equal(expected.IsRepresentRequested, actual.IsRepresentRequested);
            Assert.Equal(expected.IsRequiredInspectionRequested, actual.IsRequiredInspectionRequested);
            Assert.Equal(expected.IsComplete(), actual.IsComplete());
        }
    }

    /// <summary>
    /// Four Participations of an Event, in the shape before #615 except the last: one with times (a Representation in its
    /// first Phase, the Representation requested), one with no times at all, a final Phase only, and one already in the new
    /// shape. A Ranking and a Handout that name a Participation by its id, and two snapshot results, are there too.
    /// </summary>
    async Task<Seeded> Seed()
    {
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        var eventId = Guid.NewGuid();
        await functionsApi.Create(IntegrationPayloadFactory.EventInformation(eventId));
        var timed = TwoPhases(eventId, 1, withTimes: true);
        var untimed = TwoPhases(eventId, 2, withTimes: false);
        var final = OnlyFinal(eventId, 3);
        var alreadyNew = TwoPhases(eventId, 4, withTimes: true);
        foreach (var participation in new[] { timed, untimed, final, alreadyNew })
        {
            await functionsApi.Create(participation);
        }

        foreach (var participation in new[] { timed, untimed, final })
        {
            var document = await _stored.Read(PARTICIPATIONS, participation.Id);
            InTheOldShape(document);
            await _stored.Replace(PARTICIPATIONS, participation.Id, document);
        }

        await _stored.Insert(
            RANKINGS,
            new BsonDocument
            {
                ["_id"] = LegacyDocuments.Uuid(Guid.NewGuid()),
                ["EventId"] = LegacyDocuments.Uuid(eventId),
                ["Entries"] = new BsonArray
                {
                    new BsonDocument { ["ParticipationId"] = LegacyDocuments.Uuid(timed.Id), ["Rank"] = 1 },
                },
            }
        );
        await _stored.Insert(
            HANDOUTS,
            new BsonDocument
            {
                ["_id"] = LegacyDocuments.Uuid(Guid.NewGuid()),
                ["EventId"] = LegacyDocuments.Uuid(eventId),
                ["ParticipationId"] = LegacyDocuments.Uuid(timed.Id),
            }
        );
        await _stored.Insert(
            SNAPSHOT_RESULTS,
            new BsonDocument
            {
                ["_id"] = LegacyDocuments.Uuid(Guid.NewGuid()),
                ["EventId"] = LegacyDocuments.Uuid(eventId),
            }
        );
        await _stored.Insert(
            SNAPSHOT_RESULTS,
            new BsonDocument
            {
                ["_id"] = LegacyDocuments.Uuid(Guid.NewGuid()),
                ["EventId"] = LegacyDocuments.Uuid(eventId),
            }
        );
        return new Seeded(eventId, timed, untimed, final, alreadyNew);
    }

    /// <summary>What the code before #615 stored: the three times flat, and the Representation request as IsReinspectionRequested.</summary>
    static void InTheOldShape(BsonDocument participation)
    {
        foreach (var phase in participation["Phases"].AsBsonArray.Select(x => x.AsBsonDocument))
        {
            if (phase.TryGetValue("Events", out var events))
            {
                foreach (var @event in events.AsBsonArray.Select(x => x.AsBsonDocument))
                {
                    var isRepresent = @event.Contains("IsRepresent") && @event["IsRepresent"].AsBoolean;
                    var name =
                        @event["Kind"].AsString == "Arrived" ? "ArriveTime"
                        : isRepresent ? "RepresentTime"
                        : "PresentTime";
                    phase[name] = @event["Time"];
                }

                phase.Remove("Events");
            }

            if (phase.TryGetValue("IsRepresentRequested", out var requested))
            {
                phase.Remove("IsRepresentRequested");
                phase["IsReinspectionRequested"] = requested;
            }
        }
    }

    static Participation TwoPhases(Guid eventId, int number, bool withTimes)
    {
        var first = BuildPhase(
            "GATE1/20",
            isFinal: false,
            start: START,
            arrive: withTimes ? START.AddHours(1) : null,
            present: withTimes ? START.AddHours(1).AddMinutes(10) : null,
            represent: withTimes ? START.AddHours(1).AddMinutes(30) : null,
            isRepresentRequested: withTimes,
            id: TestId.Of(number * 100 + 1)
        );
        var final = BuildPhase(
            "GATE2/40",
            isFinal: true,
            start: withTimes ? START.AddHours(2) : null,
            arrive: withTimes ? START.AddHours(3) : null,
            present: withTimes ? START.AddHours(3).AddMinutes(10) : null,
            represent: null,
            isRepresentRequested: false,
            id: TestId.Of(number * 100 + 2)
        );
        return BuildParticipation(eventId, number, first, final);
    }

    static Participation OnlyFinal(Guid eventId, int number)
    {
        var final = BuildPhase(
            "GATE1/40",
            isFinal: true,
            start: START,
            arrive: START.AddHours(2),
            present: START.AddHours(2).AddMinutes(10),
            represent: null,
            isRepresentRequested: false,
            id: TestId.Of(number * 100 + 1)
        );
        return BuildParticipation(eventId, number, final);
    }

    static Phase BuildPhase(
        string gate,
        bool isFinal,
        DateTimeOffset? start,
        DateTimeOffset? arrive,
        DateTimeOffset? present,
        DateTimeOffset? represent,
        bool isRepresentRequested,
        Guid id
    )
    {
        return new Phase(
            gate,
            20,
            40,
            isFinal ? null : 40,
            CompetitionRuleset.FEI,
            isFinal,
            null,
            Timestamp.Create(start),
            Timestamp.Create(arrive),
            Timestamp.Create(present),
            Timestamp.Create(represent),
            isRepresentRequested,
            false,
            false,
            id
        );
    }

    static Participation BuildParticipation(Guid eventId, int number, params Phase[] phases)
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(
            "Integration Rider",
            "Integration Rider",
            country,
            null,
            null,
            TestId.Of(number * 1000 + 1)
        );
        var horse = new Horse("Integration Horse", "Integration Horse", null, TestId.Of(number * 1000 + 2));
        var combination = new Combination(number, athlete, horse, null, "40", null, null, TestId.Of(number * 1000 + 3));
        return new Participation(
            ParticipationCategory.Senior,
            new Competition("CEI 1*", CompetitionRuleset.FEI),
            combination,
            new PhaseCollection(phases),
            null,
            eventId,
            TestId.Of(number * 10 + 5)
        );
    }

    async Task<Dictionary<Guid, BsonDocument>> ParticipationsByIdAsync()
    {
        var all = await _stored.ReadAll(PARTICIPATIONS);
        return all.ToDictionary(x => x["_id"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
    }

    async Task<List<BsonDocument>> Everything()
    {
        var all = new List<BsonDocument>();
        foreach (var collection in COLLECTIONS)
        {
            all.AddRange(await _stored.ReadAll(collection));
        }

        return all;
    }

    async Task<List<string>> CollectionNames()
    {
        return await (await _database.ListCollectionNamesAsync()).ToListAsync();
    }

    static async Task<(int ExitCode, string Output, string Error)> Command(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await PhaseTimesMigrationTool.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    sealed class Seeded
    {
        public Seeded(
            Guid eventId,
            Participation timed,
            Participation untimed,
            Participation final,
            Participation alreadyNew
        )
        {
            EventId = eventId;
            Timed = timed;
            Untimed = untimed;
            Final = final;
            AlreadyNew = alreadyNew;
        }

        public Guid EventId { get; }
        public Participation Timed { get; }
        public Participation Untimed { get; }
        public Participation Final { get; }
        public Participation AlreadyNew { get; }
        public IEnumerable<Participation> All => [Timed, Untimed, Final, AlreadyNew];
    }
}
