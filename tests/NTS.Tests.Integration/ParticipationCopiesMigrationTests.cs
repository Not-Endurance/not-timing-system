using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Nexus.HTTP.Mongo;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.ParticipationCopies;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0006 and #626: <c>migrate-participation-copies</c> turns the copy of a Participation inside every Ranking entry
/// and Handout into a reference, keeps the stored ranks and strips the derived values. It runs here over documents in
/// the shape the production data has, on a MongoDB in a container; the Participations are written by the Functions API
/// and then given the derived values the code before stored.
/// </summary>
public sealed class ParticipationCopiesMigrationTests : IClassFixture<NtsIntegrationFixture>, IAsyncLifetime
{
    const string RANKINGS = MongoConstants.RANKINGS_COLLECTION;
    const string HANDOUTS = MongoConstants.HANDOUTS_COLLECTION;
    const string PARTICIPATIONS = MongoConstants.PARTICIPATIONS_COLLECTION;
    static readonly string[] COLLECTIONS = [RANKINGS, HANDOUTS, PARTICIPATIONS];
    static readonly DateTimeOffset START = new(2026, 4, 28, 8, 0, 0, TimeSpan.Zero);

    readonly NtsIntegrationFixture _fixture;
    readonly StoredDocuments _stored;
    readonly IMongoDatabase _database;

    public ParticipationCopiesMigrationTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
        _stored = new StoredDocuments(fixture.MongoConnectionString);
        _database = new MongoClient(fixture.MongoConnectionString).GetDatabase(MongoConstants.NTS_DATABASE);
    }

    /// <summary>The migration reads every document of the three collections, so each test starts from empty ones.</summary>
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
    public async Task A_dry_run_changes_nothing_and_reports_the_counts_and_the_copy_that_differs()
    {
        var seeded = await SeedLegacyEvent();
        var before = await Everything();

        var report = await Migrate(apply: false);

        Assert.Equal(before, await Everything());
        Assert.False(report.Applied);
        Assert.False(report.Refused);
        AssertCounts(report, RANKINGS, documents: 2, changed: 2, entries: 5);
        AssertCounts(report, HANDOUTS, documents: 1, changed: 1, entries: 0);
        AssertCounts(report, PARTICIPATIONS, documents: 4, changed: 4, entries: 0);
        Assert.Empty(report.Missing);
        var differing = Assert.Single(report.Differing);
        Assert.Equal(RANKINGS, differing.Collection);
        Assert.Equal(LegacyDocuments.Uuid(seeded.SecondRanking), differing.DocumentId);
        Assert.Equal("Entries[0]", differing.Where);
        Assert.Equal(LegacyDocuments.Uuid(seeded.Fourth.Id), differing.ParticipationId);
        Assert.Equal(["Phases[0].PresentTime"], differing.Fields); // the copy was taken before it was presented
    }

    [Fact]
    public async Task Apply_turns_the_entries_and_the_Handout_into_references_keeps_the_ranks_and_strips_the_derived_values()
    {
        var seeded = await SeedLegacyEvent();
        var participationsBefore = await _stored.ReadAll(PARTICIPATIONS);

        var report = await Migrate(apply: true);

        Assert.True(report.Applied);
        AssertCounts(report, RANKINGS, documents: 2, changed: 2, entries: 5);
        AssertCounts(report, HANDOUTS, documents: 1, changed: 1, entries: 0);
        AssertCounts(report, PARTICIPATIONS, documents: 4, changed: 4, entries: 0);

        var first = await _stored.Read(RANKINGS, seeded.FirstRanking);
        Assert.Equal("CEI 1*", first["Name"].AsString); // the header is left as it was
        Assert.Equal(
            [
                new BsonDocument { ["ParticipationId"] = LegacyDocuments.Uuid(seeded.Third.Id), ["Rank"] = 2 },
                new BsonDocument { ["ParticipationId"] = LegacyDocuments.Uuid(seeded.First.Id) },
                new BsonDocument
                {
                    ["ParticipationId"] = LegacyDocuments.Uuid(seeded.Second.Id),
                    ["IsNotRanked"] = true,
                },
            ],
            first["Entries"].AsBsonArray.Select(x => x.AsBsonDocument)
        );
        var second = await _stored.Read(RANKINGS, seeded.SecondRanking);
        Assert.Equal(
            [
                new BsonDocument { ["ParticipationId"] = LegacyDocuments.Uuid(seeded.Fourth.Id) },
                new BsonDocument { ["ParticipationId"] = LegacyDocuments.Uuid(seeded.Second.Id) },
            ],
            second["Entries"].AsBsonArray.Select(x => x.AsBsonDocument)
        );

        var handout = await _stored.Read(HANDOUTS, seeded.Handout);
        Assert.DoesNotContain("Participation", handout.Names);
        StoredDocuments.AssertStandardUuid(handout["ParticipationId"], seeded.First.Id);
        StoredDocuments.AssertStandardUuid(handout["EventId"], seeded.EventId);
        Assert.Equal("nts", handout["TenantId"].AsString);

        foreach (var before in participationsBefore)
        {
            var after = await _stored.Read(
                PARTICIPATIONS,
                before["_id"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard)
            );
            Assert.Equal(LegacyDocuments.WithoutDerivedValues(before), after); // nothing else was touched
        }
    }

    [Fact]
    public async Task The_Results_composed_after_the_migration_equal_the_ones_before_for_every_entry_whose_copy_did_not_differ()
    {
        var seeded = await SeedLegacyEvent();
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        var copies = await functionsApi.ReadParticipations(seeded.EventId); // the copies of before are the stored Participations
        var firstBefore = new Result(
            CreateRanking(
                seeded.FirstRanking,
                seeded.EventId,
                new RankingEntry(seeded.Third.Id, false, 2),
                new RankingEntry(seeded.First.Id, false),
                new RankingEntry(seeded.Second.Id, true)
            ),
            copies
        );

        await Migrate(apply: true);

        var rankings = await functionsApi.ReadRankings(seeded.EventId);
        var participations = await functionsApi.ReadParticipations(seeded.EventId);
        var firstAfter = new Result(rankings.Single(x => x.Id == seeded.FirstRanking), participations);
        var secondAfter = new Result(rankings.Single(x => x.Id == seeded.SecondRanking), participations);

        Assert.Equal(Rows(firstBefore), Rows(firstAfter));
        Assert.Equal(
            [seeded.First.Id, seeded.Third.Id, seeded.Second.Id],
            firstAfter.Entries.Select(x => x.ParticipationId)
        ); // two ranked by arrival, the not ranked one last
        Assert.Equal([1, 2, 3], firstAfter.Entries.Select(x => x.Rank));
        Assert.Equal([seeded.Second.Id, seeded.Fourth.Id], secondAfter.Entries.Select(x => x.ParticipationId));
        var secondBefore = copies.Single(x => x.Id == seeded.Second.Id).ToString();
        Assert.Equal(secondBefore, secondAfter.Entries[0].Participation.ToString()); // the same entry in the other Ranking
        Assert.NotNull(secondAfter.Entries[1].Participation.Phases[0].PresentTime); // the copy lacked it, the stored one has it
    }

    [Fact]
    public async Task Applying_a_second_time_changes_nothing()
    {
        await SeedLegacyEvent();
        await Migrate(apply: true);
        var afterTheFirst = await Everything();

        var report = await Migrate(apply: true);

        Assert.Equal(afterTheFirst, await Everything());
        Assert.True(report.Applied);
        Assert.All(report.Collections, counts => Assert.Equal(0, counts.Changed));
        Assert.Empty(report.Missing);
        Assert.Empty(report.Differing);
    }

    [Fact]
    public async Task Apply_refuses_while_a_Participation_is_missing_and_says_which()
    {
        var dangling = await SeedDanglingReferences(await SeedLegacyEvent());
        var before = await Everything();

        var dryRun = await Migrate(apply: false);
        var refused = await Migrate(apply: true);

        Assert.False(dryRun.Refused); // a dry run reports it and goes on
        Assert.True(refused.Refused);
        Assert.False(refused.Applied);
        Assert.Equal(before, await Everything()); // nothing was changed
        Assert.Equal(dangling.Expected.Order(), Describe(dryRun).Order());
        Assert.Equal(dangling.Expected.Order(), Describe(refused).Order());
        var said = new StringWriter();
        refused.WriteTo(said);
        Assert.Contains(dangling.Nowhere.ToString(), said.ToString());
        Assert.Contains(dangling.NowhereElse.ToString(), said.ToString());
        Assert.Contains(dangling.Elsewhere.ToString(), said.ToString());
        Assert.Contains("Refusing to apply", said.ToString());
    }

    [Fact]
    public async Task A_copy_that_differs_in_several_ways_lists_each_field_and_an_integer_equal_to_a_double_is_no_difference()
    {
        var seeded = await SeedLegacyEvent();
        var stored = seeded.Copy(seeded.Second);
        var copy = stored.DeepClone().AsBsonDocument; // the copy as it was taken
        copy["Phases"].AsBsonArray[0].AsBsonDocument["Length"] = new BsonInt32(40); // stored as a double, 40.0
        stored["Eliminated"] = new BsonDocument { ["Kind"] = "Withdrawn" }; // eliminated since
        stored["Phases"].AsBsonArray.Add(stored["Phases"].AsBsonArray[0].DeepClone()); // a Phase added since
        stored["Phases"].AsBsonArray[0].AsBsonDocument["StartTime"] = new BsonDateTime(START.AddMinutes(1).UtcDateTime);
        await _stored.Replace(PARTICIPATIONS, seeded.Second.Id, stored);
        var third = Guid.NewGuid();
        await _stored.Insert(
            RANKINGS,
            LegacyDocuments.Ranking(
                LegacyDocuments.Uuid(third),
                LegacyDocuments.Uuid(seeded.EventId),
                "CEI 3*",
                LegacyDocuments.RankingEntry(copy)
            )
        );

        var report = await Migrate(apply: false);

        var differing = Assert.Single(report.Differing, x => x.DocumentId == LegacyDocuments.Uuid(third));
        Assert.Equal(["Eliminated", "Phases", "Phases[0].StartTime"], differing.Fields);
    }

    [Fact]
    public async Task A_Ranking_that_lists_a_Participation_twice_is_reported_and_apply_refuses_it()
    {
        var seeded = await SeedLegacyEvent();
        var twice = Guid.NewGuid();
        await _stored.Insert(
            RANKINGS,
            LegacyDocuments.Ranking(
                LegacyDocuments.Uuid(twice),
                LegacyDocuments.Uuid(seeded.EventId),
                "CEI 3*",
                LegacyDocuments.RankingEntry(seeded.Copy(seeded.First)),
                LegacyDocuments.RankingEntry(seeded.Copy(seeded.First), isNotRanked: true)
            )
        );
        var before = await Everything();

        var dryRun = await Migrate(apply: false);
        var refused = await Migrate(apply: true);

        var repeated = Assert.Single(dryRun.Repeated);
        Assert.Equal(RANKINGS, repeated.Collection);
        Assert.Equal(LegacyDocuments.Uuid(twice), repeated.DocumentId);
        Assert.Equal(LegacyDocuments.Uuid(seeded.First.Id), repeated.ParticipationId);
        Assert.Empty(dryRun.Missing);
        Assert.False(dryRun.Refused);
        Assert.True(refused.Refused); // the new code does not load a Ranking that lists one twice
        Assert.False(refused.Applied);
        Assert.Equal(before, await Everything());
        var said = new StringWriter();
        refused.WriteTo(said);
        Assert.Contains(seeded.First.Id.ToString(), said.ToString());
    }

    [Fact]
    public async Task The_golden_Event_in_the_shape_of_before_converts_to_the_references_of_its_golden_Rankings()
    {
        // A real finished Event, with the integer ids of before. Its Participations carry what the old code stored; the
        // Rankings of before are put together from its golden references and the Participation each one names.
        var directory = Path.Combine(
            RepositoryPaths.Discover().Root,
            "tests",
            "NTS.Tests.Integration",
            "EndToEndEventTests",
            "Snapshots",
            "2026-03-Asenovgrad"
        );
        var participations = await ReadGolden(directory, "nts.event_participations.json");
        var golden = await ReadGolden(directory, "nts.event_rankings.json");
        var byId = participations.ToDictionary(x => x["_id"].AsInt32);
        foreach (var participation in participations)
        {
            await _stored.Insert(PARTICIPATIONS, participation.DeepClone().AsBsonDocument);
        }

        foreach (var ranking in golden)
        {
            var before = ranking.DeepClone().AsBsonDocument;
            before["Entries"] = new BsonArray(
                ranking["Entries"]
                    .AsBsonArray.Select(entry =>
                        LegacyDocuments.RankingEntry(
                            byId[entry["ParticipationId"].AsInt32],
                            entry["Rank"].IsBsonNull ? null : entry["Rank"].AsInt32,
                            entry["IsNotRanked"].AsBoolean
                        )
                    )
            );
            await _stored.Insert(RANKINGS, before);
        }

        var report = await Migrate(apply: true);

        Assert.Empty(report.Missing);
        Assert.Empty(report.Repeated);
        Assert.Empty(report.Differing); // the copies are the stored Participations
        AssertCounts(report, RANKINGS, documents: 7, changed: 7, entries: 26);
        Assert.Equal(golden.Select(WithoutDefaults), (await _stored.ReadAll(RANKINGS)).Select(WithoutDefaults));
        Assert.Equal(
            participations.Select(LegacyDocuments.WithoutDerivedValues),
            await _stored.ReadAll(PARTICIPATIONS)
        );
    }

    [Fact]
    public async Task The_command_exits_with_1_when_it_refuses_or_is_asked_wrongly_and_with_0_otherwise()
    {
        await SeedDanglingReferences(await SeedLegacyEvent());
        var connection = _fixture.MongoConnectionString;

        var dryRun = await Command(["--connection-string", connection]);
        var refused = await Command(["--connection-string", connection, "--apply"]);
        var wrongly = await Command(["--apply"]);
        var help = await Command(["--help"]);

        Assert.Equal(0, dryRun.ExitCode);
        Assert.Contains("Participations missing: 4", dryRun.Output);
        Assert.Contains("Nothing was changed. Run again with --apply to persist.", dryRun.Output);
        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("Refusing to apply", refused.Output);
        Assert.Equal(1, wrongly.ExitCode);
        Assert.Contains("--connection-string is required.", wrongly.Error);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("migrate-participation-copies", help.Output);
    }

    [Fact]
    public async Task Documents_with_the_integer_ids_of_before_get_references_with_the_same_integers()
    {
        var event1 = new BsonInt32(1);
        var first = LegacyDocuments.Participation(new BsonInt32(21), event1, 1);
        var second = LegacyDocuments.Participation(new BsonInt32(22), event1, 2);
        await _stored.Insert(PARTICIPATIONS, first.DeepClone().AsBsonDocument);
        await _stored.Insert(PARTICIPATIONS, second.DeepClone().AsBsonDocument);
        await _stored.Insert(
            RANKINGS,
            LegacyDocuments.Ranking(
                new BsonInt32(11),
                event1,
                "ДП пони ездачи",
                LegacyDocuments.RankingEntry(first),
                LegacyDocuments.RankingEntry(second, rank: 3, isNotRanked: true)
            )
        );
        await _stored.Insert(HANDOUTS, LegacyDocuments.Handout(new BsonInt32(31), event1, second));

        var report = await Migrate(apply: true);

        Assert.Empty(report.Missing);
        Assert.Empty(report.Differing);
        var ranking = Assert.Single(await _stored.ReadAll(RANKINGS));
        Assert.Equal(
            [
                new BsonDocument { ["ParticipationId"] = 21 },
                new BsonDocument
                {
                    ["ParticipationId"] = 22,
                    ["IsNotRanked"] = true,
                    ["Rank"] = 3,
                },
            ],
            ranking["Entries"].AsBsonArray.Select(x => x.AsBsonDocument)
        );
        Assert.Equal("ДП пони ездачи", ranking["Name"].AsString);
        var handout = Assert.Single(await _stored.ReadAll(HANDOUTS));
        Assert.Equal(22, handout["ParticipationId"].AsInt32);
        Assert.DoesNotContain("Participation", handout.Names);
        Assert.Equal(
            [LegacyDocuments.WithoutDerivedValues(first), LegacyDocuments.WithoutDerivedValues(second)],
            await _stored.ReadAll(PARTICIPATIONS)
        );
    }

    static List<(string, Guid, string, Guid?)> Describe(ParticipationCopiesReport report)
    {
        return report
            .Missing.Select(x =>
                (
                    x.Collection,
                    x.DocumentId.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard),
                    x.Where,
                    x.ParticipationId?.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard)
                )
            )
            .ToList();
    }

    async Task<(int ExitCode, string Output, string Error)> Command(string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await ParticipationCopiesMigrationTool.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    /// References to a Participation that is not there, four ways: a copy of one that is nowhere, an entry that holds
    /// no copy, a copy of one that belongs to another Event, and a Handout with a copy of one that is nowhere.
    /// </summary>
    async Task<DanglingReferences> SeedDanglingReferences(SeededEvent seeded)
    {
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        var otherEvent = Guid.NewGuid();
        await functionsApi.Create(IntegrationPayloadFactory.EventInformation(otherEvent));
        var elsewhere = await StoreLegacy(functionsApi, Finished(otherEvent, 5, START.AddHours(3)));
        var dangling = new DanglingReferences(elsewhere["_id"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
        var invented = seeded.Copy(seeded.First).DeepClone().AsBsonDocument;
        invented["_id"] = LegacyDocuments.Uuid(dangling.Nowhere);
        var inventedForHandout = invented.DeepClone().AsBsonDocument;
        inventedForHandout["_id"] = LegacyDocuments.Uuid(dangling.NowhereElse);
        await _stored.Insert(
            RANKINGS,
            LegacyDocuments.Ranking(
                LegacyDocuments.Uuid(dangling.Third),
                LegacyDocuments.Uuid(seeded.EventId),
                "CEI 3*",
                LegacyDocuments.RankingEntry(invented),
                new BsonDocument { ["Rank"] = BsonNull.Value, ["IsNotRanked"] = false }
            )
        );
        await _stored.Insert(
            RANKINGS,
            LegacyDocuments.Ranking(
                LegacyDocuments.Uuid(dangling.Fourth),
                LegacyDocuments.Uuid(seeded.EventId),
                "CEI 4*",
                LegacyDocuments.RankingEntry(elsewhere)
            )
        );
        await _stored.Insert(
            HANDOUTS,
            LegacyDocuments.Handout(
                LegacyDocuments.Uuid(dangling.Handout),
                LegacyDocuments.Uuid(seeded.EventId),
                inventedForHandout
            )
        );
        return dangling;
    }

    static async Task<List<BsonDocument>> ReadGolden(string directory, string file)
    {
        var json = await File.ReadAllTextAsync(Path.Combine(directory, file));
        return BsonDocument
            .Parse("{ items: " + json + " }")["items"]
            .AsBsonArray.Select(x => x.AsBsonDocument)
            .ToList();
    }

    /// <summary>The Ranking as the application writes it: an entry's null rank and false mark are not there.</summary>
    static BsonDocument WithoutDefaults(BsonDocument ranking)
    {
        var simplified = ranking.DeepClone().AsBsonDocument;
        foreach (var entry in simplified["Entries"].AsBsonArray.Select(x => x.AsBsonDocument))
        {
            if (entry.GetValue("Rank", BsonNull.Value).IsBsonNull)
            {
                entry.Remove("Rank");
            }

            if (!entry.GetValue("IsNotRanked", false).AsBoolean)
            {
                entry.Remove("IsNotRanked");
            }
        }

        return simplified;
    }

    static Participation Finished(Guid eventId, int number, DateTimeOffset arrive)
    {
        var participation = IntegrationPayloadFactory.ActiveParticipation(
            eventId,
            number,
            Guid.NewGuid(),
            startTime: START
        );
        participation.Process(IntegrationPayloadFactory.ArriveSnapshot(number, arrive));
        participation.Process(IntegrationPayloadFactory.PresentSnapshot(number, arrive.AddMinutes(10)));
        return participation;
    }

    static Ranking CreateRanking(Guid id, Guid eventId, params RankingEntry[] entries)
    {
        return new Ranking(
            "CEI 1*",
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            eventId,
            id
        );
    }

    static List<string> Rows(Result results)
    {
        return results.Entries.Select(x => $"{x.ParticipationId}|{x.IsNotRanked}|{x.Rank}|{x.Participation}").ToList();
    }

    static void AssertCounts(
        ParticipationCopiesReport report,
        string collection,
        int documents,
        int changed,
        int entries
    )
    {
        var counts = report.Collections.Single(x => x.Collection == collection);
        Assert.Equal((documents, changed, entries), (counts.Documents, counts.Changed, counts.Entries));
    }

    Task<ParticipationCopiesReport> Migrate(bool apply)
    {
        return ParticipationCopiesMigration.Run(_database, apply);
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

    /// <summary>Writes the Participation through the Functions API, then gives it the derived values of before.</summary>
    async Task<BsonDocument> StoreLegacy(FunctionsApiDriver functionsApi, Participation participation)
    {
        await functionsApi.Create(participation);
        var stored = await _stored.Read(PARTICIPATIONS, participation.Id);
        LegacyDocuments.AddDerivedValues(stored);
        await _stored.Replace(PARTICIPATIONS, participation.Id, stored);
        return stored;
    }

    /// <summary>
    /// An Event of four Participations that finished half an hour apart; two Rankings that count them (the second
    /// Participation in both, once marked not ranked) with a stored rank on one entry; and a Handout. The copy of the
    /// fourth in the second Ranking was taken before it was presented, so it is stale.
    /// </summary>
    async Task<SeededEvent> SeedLegacyEvent()
    {
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        var eventId = Guid.NewGuid();
        await functionsApi.Create(IntegrationPayloadFactory.EventInformation(eventId));
        var participations = new List<Participation>();
        var copies = new Dictionary<Guid, BsonDocument>();
        for (var number = 1; number <= 4; number++)
        {
            var participation = Finished(eventId, number, START.AddHours(1).AddMinutes(30 * (number - 1)));
            participations.Add(participation);
            copies[participation.Id] = await StoreLegacy(functionsApi, participation);
        }

        var seeded = new SeededEvent(eventId, participations, copies);
        var staleFourth = seeded.Copy(seeded.Fourth).DeepClone().AsBsonDocument;
        staleFourth["Phases"].AsBsonArray[0].AsBsonDocument.Remove("PresentTime");
        await _stored.Insert(
            RANKINGS,
            LegacyDocuments.Ranking(
                LegacyDocuments.Uuid(seeded.FirstRanking),
                LegacyDocuments.Uuid(eventId),
                "CEI 1*",
                LegacyDocuments.RankingEntry(seeded.Copy(seeded.Third), rank: 2),
                LegacyDocuments.RankingEntry(seeded.Copy(seeded.First)),
                LegacyDocuments.RankingEntry(seeded.Copy(seeded.Second), isNotRanked: true)
            )
        );
        await _stored.Insert(
            RANKINGS,
            LegacyDocuments.Ranking(
                LegacyDocuments.Uuid(seeded.SecondRanking),
                LegacyDocuments.Uuid(eventId),
                "CEI 2*",
                LegacyDocuments.RankingEntry(staleFourth),
                LegacyDocuments.RankingEntry(seeded.Copy(seeded.Second))
            )
        );
        await _stored.Insert(
            HANDOUTS,
            LegacyDocuments.Handout(
                LegacyDocuments.Uuid(seeded.Handout),
                LegacyDocuments.Uuid(eventId),
                seeded.Copy(seeded.First)
            )
        );
        return seeded;
    }

    sealed class DanglingReferences
    {
        public DanglingReferences(Guid elsewhere)
        {
            Elsewhere = elsewhere;
        }

        public Guid Elsewhere { get; }
        public Guid Nowhere { get; } = Guid.NewGuid();
        public Guid NowhereElse { get; } = Guid.NewGuid();
        public Guid Third { get; } = Guid.NewGuid();
        public Guid Fourth { get; } = Guid.NewGuid();
        public Guid Handout { get; } = Guid.NewGuid();

        /// <summary>What the report lists as missing: the collection, the document, the place, the Participation.</summary>
        public IEnumerable<(string, Guid, string, Guid?)> Expected =>
            [
                (RANKINGS, Third, "Entries[0]", Nowhere),
                (RANKINGS, Third, "Entries[1]", null),
                (RANKINGS, Fourth, "Entries[0]", Elsewhere),
                (HANDOUTS, Handout, "Participation", NowhereElse),
            ];
    }

    sealed class SeededEvent
    {
        readonly Dictionary<Guid, BsonDocument> _copies;

        public SeededEvent(
            Guid eventId,
            IReadOnlyList<Participation> participations,
            Dictionary<Guid, BsonDocument> copies
        )
        {
            EventId = eventId;
            First = participations[0];
            Second = participations[1];
            Third = participations[2];
            Fourth = participations[3];
            _copies = copies;
        }

        public Guid EventId { get; }
        public Participation First { get; }
        public Participation Second { get; }
        public Participation Third { get; }
        public Participation Fourth { get; }
        public Guid FirstRanking { get; } = Guid.NewGuid();
        public Guid SecondRanking { get; } = Guid.NewGuid();
        public Guid Handout { get; } = Guid.NewGuid();

        /// <summary>The stored document of the Participation, with the derived values of before: its copy.</summary>
        public BsonDocument Copy(Participation participation)
        {
            return _copies[participation.Id];
        }
    }
}
