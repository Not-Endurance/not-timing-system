using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Reference;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Harness.Drivers;
using NTS.Tests.Integration.Harness;
using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0006, #640, #637: the cutover leaves every Event that has ended with the final placings of its Rankings. The documents are made
/// by the code of today and turned into the documents of before the Guids, so that they are ones the Results code can read once they
/// are converted. The placings are what the Results compute; one that is stored is never written again; a Ranking that holds some of
/// its ranks, or that the domain refuses, is told and left alone.
/// </summary>
public sealed class IdentitiesApplyRankingsTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset ARRIVE = LegacyWorldData.NOW.AddHours(-30);
    static readonly Guid EVENT = LegacyGuids.Of("Event", null, 900);

    readonly MongoFixture _mongo;

    public IdentitiesApplyRankingsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Every_Ranking_of_an_Event_that_has_ended_gets_the_ranks_the_Results_compute_and_a_single_entry_is_rank_1()
    {
        var (database, data) = await SeededAsync();

        var result = await ApplyAsync(database);

        Assert.True(
            result.Succeeded,
            string.Join("; ", result.Refusals.Concat(result.Checks.SelectMany(x => x.Details)))
        );
        Assert.Equal(3, result.Rankings.Finalised.Count);
        foreach (var ranking in new[] { data.Everyone, data.WithoutTheFirst, data.OfOne })
        {
            var stored = await RanksOfAsync(database, data.Shape.NumberOf(ranking.Id));
            Assert.Equal(data.ExpectedRanks(ranking), stored);
        }

        var everyone = await RanksOfAsync(database, data.Shape.NumberOf(data.Everyone.Id));
        Assert.Equal(new int?[] { 1, 2, 3, 4 }, new[] { 1, 2, 3, 4 }.Select(x => everyone[x])); // the eliminated is last, as the Results show it
        Assert.Equal(1, (await RanksOfAsync(database, data.Shape.NumberOf(data.OfOne.Id)))[2]);
    }

    [Fact]
    public async Task A_placing_that_is_stored_is_never_written_again_and_a_Ranking_with_some_ranks_is_told_and_left_alone()
    {
        var (database, data) = await SeededAsync();

        var result = await ApplyAsync(database);

        var stored = await RanksOfAsync(database, data.Shape.NumberOf(data.Stored.Id));
        Assert.Equal(new Dictionary<int, int?> { [1] = 2, [2] = 1 }, stored); // the reverse of what the times give
        Assert.Contains(
            LegacyGuids.Of("Ranking", EVENT, data.Shape.NumberOf(data.Stored.Id)),
            result.Rankings.AlreadyFinal
        );
        var partial = await RanksOfAsync(database, data.Shape.NumberOf(data.Partial.Id));
        Assert.Equal(
            new Dictionary<int, int?>
            {
                [1] = 1,
                [2] = null,
                [3] = null,
                [4] = null,
            },
            partial
        );
        Assert.Equal(
            [LegacyGuids.Of("Ranking", EVENT, data.Shape.NumberOf(data.Partial.Id))],
            result.Rankings.SomePlacingsStored
        );
        var check = Assert.Single(result.Checks, x => x.Name.Contains("placings", StringComparison.Ordinal));
        Assert.True(check.Passed);
        Assert.Contains(check.Details, x => x.Contains("only some of its ranks"));
    }

    [Fact]
    public async Task A_Ranking_of_an_Event_that_is_still_Live_is_left_without_ranks()
    {
        var (database, data) = await SeededAsync();

        await ApplyAsync(database);

        var live = await RanksOfAsync(database, data.Shape.NumberOf(data.Live.Id), 903);
        Assert.All(live.Values, x => Assert.Null(x));
    }

    [Fact]
    public async Task A_Ranking_that_counts_a_Participation_that_is_not_there_is_refused_and_told_and_the_others_are_done()
    {
        var (database, data) = await SeededAsync();
        var dangling = new Ranking(
            "Dangling",
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            [new RankingEntry(Guid.NewGuid(), false)],
            data.Event,
            Guid.NewGuid()
        );
        await Rankings(database).InsertOneAsync(data.Shape.Downgrade(RankingModel.From(dangling).ToBsonDocument()));

        var result = await ApplyAsync(database);

        Assert.Equal(3, result.Rankings.Finalised.Count);
        Assert.Equal([LegacyGuids.Of("Ranking", EVENT, data.Shape.NumberOf(dangling.Id))], result.Rankings.Refused);
        var placings = Assert.Single(result.Checks, x => x.Name.Contains("placings", StringComparison.Ordinal));
        Assert.True(placings.Passed); // told, and not a failure of the cutover
        Assert.Contains(placings.Details, x => x.Contains("could not be finalised"));
        var references = Assert.Single(result.Checks, x => !x.Passed);
        Assert.Equal("Every reference resolves", references.Name); // and the entry that names nothing is told by the check of references
    }

    [Fact]
    public async Task A_rank_that_another_writer_stores_while_the_placings_are_made_is_not_written_over()
    {
        var (database, data) = await SeededAsync();
        var everyone = LegacyGuids.Of("Ranking", EVENT, data.Shape.NumberOf(data.Everyone.Id));

        var result = await IdentitiesMigration.Apply(
            database,
            IdentityDecisions.None,
            new ApplyOptions
            {
                Now = LegacyWorldData.NOW,
                BeforeRankingWritten = async ranking =>
                {
                    if (ranking == everyone) // the sweep of the Api, say, got there first
                    {
                        await Rankings(database)
                            .UpdateOneAsync(
                                new BsonDocument("_id", new BsonBinaryData(everyone, GuidRepresentation.Standard)),
                                new BsonDocument("$set", new BsonDocument("Entries.0.Rank", 9))
                            );
                    }
                },
            }
        );

        Assert.DoesNotContain(everyone, result.Rankings.Finalised);
        Assert.Equal(2, result.Rankings.Finalised.Count);
        var stored = await RanksOfAsync(database, data.Shape.NumberOf(data.Everyone.Id));
        Assert.Equal(9, stored[4]); // the first entry is the eliminated ride
        Assert.Null(stored[3]);
    }

    [Fact]
    public async Task A_Ranking_that_is_deleted_and_the_Rankings_of_an_Event_that_is_deleted_are_left_alone()
    {
        var (database, data) = await SeededAsync();
        var deleted = RankingOf(
            data.Event,
            "Deleted",
            [new RankingEntry(data.Everyone.Entries[0].ParticipationId, false)]
        );
        var ofDeletedEvent = RankingOf(Guid.NewGuid(), "Of a deleted Event", [new RankingEntry(Guid.NewGuid(), false)]);
        var deletedEvent = data.Shape.NumberOf(ofDeletedEvent.EventId);
        await Insert(
            database,
            "event_rankings",
            data.Shape.Downgrade(RankingModel.From(deleted).ToBsonDocument()).Add("IsDeleted", true),
            data.Shape.Downgrade(RankingModel.From(ofDeletedEvent).ToBsonDocument())
        );
        var start = LegacyWorldData.NOW.UtcDateTime.AddDays(-3);
        var information = LegacyWorldData.Information(deletedEvent, "Deleted ride", start, start.AddDays(1));
        information["IsDeleted"] = true;
        await Insert(database, "event_informations", information);

        var result = await ApplyAsync(database);

        Assert.Equal(3, result.Rankings.Finalised.Count);
        Assert.Empty(result.Rankings.Refused);
        var placings = Assert.Single(result.Checks, x => x.Name.Contains("placings", StringComparison.Ordinal));
        Assert.True(placings.Passed); // a Ranking that is deleted is not one the verification asks placings of
        var untouched = await Rankings(database)
            .Find(new BsonDocument("Name", new BsonDocument("$in", new BsonArray { "Deleted", "Of a deleted Event" })))
            .ToListAsync();
        Assert.Equal(2, untouched.Count);
        Assert.All(
            untouched,
            x => Assert.All(x["Entries"].AsBsonArray, y => Assert.False(y.AsBsonDocument.Contains("Rank")))
        );
    }

    [Fact]
    public async Task A_Participation_that_is_deleted_is_not_given_to_the_Results_so_a_Ranking_that_counts_it_is_refused()
    {
        var (database, data) = await SeededAsync();
        var deletedRide = IntegrationPayloadFactory.CompletedParticipation(
            data.Event,
            5,
            Guid.NewGuid(),
            ARRIVE.AddMinutes(30)
        );
        var counting = RankingOf(
            data.Event,
            "Counts a deleted ride",
            [new RankingEntry(deletedRide.Id, false), new RankingEntry(data.Everyone.Entries[0].ParticipationId, false)]
        );
        await Insert(
            database,
            "event_participations",
            data.Shape.Downgrade(ParticipationModel.MapFrom(deletedRide).ToBsonDocument()).Add("IsDeleted", true)
        );
        await Insert(database, "event_rankings", data.Shape.Downgrade(RankingModel.From(counting).ToBsonDocument()));

        var result = await ApplyAsync(database);

        Assert.Equal([LegacyGuids.Of("Ranking", EVENT, data.Shape.NumberOf(counting.Id))], result.Rankings.Refused);
        Assert.Equal(3, result.Rankings.Finalised.Count);
    }

    [Theory]
    [InlineData("""{ "OnlyAverageLoopSpeed": false, "RankerCode": "" }""")] // a code that is no code
    [InlineData("""{ "OnlyAverageLoopSpeed": "sometimes" }""")] // a flag that is no flag
    public async Task An_Event_whose_rules_cannot_be_read_leaves_its_Rankings_without_ranks_and_told(string rules)
    {
        var (database, data) = await SeededAsync();
        await database
            .GetCollection<BsonDocument>("event_informations")
            .UpdateOneAsync(
                new BsonDocument("_id", 900),
                new BsonDocument("$set", new BsonDocument("RegionalRules", BsonDocument.Parse(rules)))
            );

        var result = await ApplyAsync(database);

        Assert.Empty(result.Rankings.Finalised);
        Assert.Equal(
            new[] { data.Everyone, data.WithoutTheFirst, data.OfOne }
                .Select(x => LegacyGuids.Of("Ranking", EVENT, data.Shape.NumberOf(x.Id)))
                .Order(),
            result.Rankings.Refused.Order()
        );
        var placings = Assert.Single(result.Checks, x => x.Name.Contains("placings", StringComparison.Ordinal));
        Assert.True(placings.Passed); // told, and not a failure of the cutover
        Assert.Contains(placings.Details, x => x.Contains("could not be finalised"));
    }

    [Fact]
    public async Task A_Participation_that_cannot_be_read_is_left_out_and_the_Rankings_that_count_it_are_refused()
    {
        var (database, data) = await SeededAsync();
        var incomplete = data.Everyone.Entries[1].ParticipationId; // counted by one Ranking to finalise
        await database
            .GetCollection<BsonDocument>("event_participations")
            .UpdateOneAsync(
                new BsonDocument("_id", data.Shape.NumberOf(incomplete)),
                new BsonDocument("$set", new BsonDocument("Combination.Number", "not a number"))
            );

        var result = await ApplyAsync(database);

        Assert.Equal(
            [LegacyGuids.Of("Ranking", EVENT, data.Shape.NumberOf(data.Everyone.Id))],
            result.Rankings.Refused
        );
        Assert.Equal(2, result.Rankings.Finalised.Count);
    }

    [Fact]
    public async Task Reading_in_pages_of_two_documents_gives_the_same_placings()
    {
        var (database, data) = await SeededAsync();

        var result = await IdentitiesMigration.Apply(
            database,
            IdentityDecisions.None,
            new ApplyOptions { Now = LegacyWorldData.NOW, PageSize = 2 }
        );

        Assert.True(
            result.Succeeded,
            string.Join("; ", result.Refusals.Concat(result.Checks.SelectMany(x => x.Details)))
        );
        Assert.Equal(3, result.Rankings.Finalised.Count);
        Assert.Equal(
            data.ExpectedRanks(data.Everyone),
            await RanksOfAsync(database, data.Shape.NumberOf(data.Everyone.Id))
        );
    }

    [Fact]
    public async Task A_Ranking_of_an_Event_that_has_ended_without_placings_fails_the_verification()
    {
        var (database, data) = await SeededAsync();
        var everyone = LegacyGuids.Of("Ranking", EVENT, data.Shape.NumberOf(data.Everyone.Id));

        var result = await IdentitiesMigration.Apply(
            database,
            IdentityDecisions.None,
            new ApplyOptions
            {
                Now = LegacyWorldData.NOW,
                AfterStep = async step =>
                {
                    if (step == "rankings") // the placings go, as if they had never been stored
                    {
                        await Rankings(database)
                            .UpdateOneAsync(
                                new BsonDocument("_id", new BsonBinaryData(everyone, GuidRepresentation.Standard)),
                                new BsonDocument("$unset", new BsonDocument("Entries.$[].Rank", ""))
                            );
                    }
                },
            }
        );

        Assert.False(result.Succeeded);
        var failed = Assert.Single(result.Checks, x => !x.Passed);
        Assert.Contains("placings", failed.Name);
        Assert.Contains(failed.Details, x => x.Contains(everyone.ToString()) && x.Contains("has no placings"));
    }

    [Fact]
    public async Task Running_apply_again_stores_nothing_more()
    {
        var (database, _) = await SeededAsync();
        await ApplyAsync(database);
        var once = await LegacyWorldData.SnapshotAsync(database);

        var again = await ApplyAsync(database);

        Assert.True(again.Succeeded);
        Assert.Empty(again.Rankings.Finalised);
        Assert.Equal(once, await LegacyWorldData.SnapshotAsync(database));
    }

    static IMongoCollection<BsonDocument> Rankings(IMongoDatabase database)
    {
        return database.GetCollection<BsonDocument>("event_rankings");
    }

    static Task<ApplyResult> ApplyAsync(IMongoDatabase database)
    {
        return IdentitiesMigration.Apply(
            database,
            IdentityDecisions.None,
            new ApplyOptions { Now = LegacyWorldData.NOW }
        );
    }

    /// <summary>The ranks of a Ranking by the number of the ride in the start list; none for a ride that holds none.</summary>
    static async Task<Dictionary<int, int?>> RanksOfAsync(IMongoDatabase database, int ranking, int ofEvent = 900)
    {
        var document = await Rankings(database)
            .Find(
                new BsonDocument(
                    "_id",
                    new BsonBinaryData(
                        LegacyGuids.Of("Ranking", LegacyGuids.Of("Event", null, ofEvent), ranking),
                        GuidRepresentation.Standard
                    )
                )
            )
            .SingleAsync();
        var numbers = new Dictionary<Guid, int>();
        foreach (
            var ride in await database
                .GetCollection<BsonDocument>("event_participations")
                .Find(FilterDefinition<BsonDocument>.Empty)
                .ToListAsync()
        )
        {
            numbers[ride["_id"].AsGuid] = ride["Combination"]["Number"].AsInt32;
        }

        return document["Entries"]
            .AsBsonArray.Select(x => x.AsBsonDocument)
            .ToDictionary(
                x => numbers[x["ParticipationId"].AsGuid],
                x => x.TryGetValue("Rank", out var rank) && !rank.IsBsonNull ? (int?)rank.AsInt32 : null
            );
    }

    async Task<(IMongoDatabase Database, Seeded Data)> SeededAsync()
    {
        ApiMongo.Configure();
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase("apply-" + Guid.NewGuid().ToString("N"));
        var shape = new LegacyShape();
        var eventId = Guid.NewGuid();
        var liveId = Guid.NewGuid();
        shape.Name(eventId, 900);
        shape.Name(liveId, 903);

        var first = IntegrationPayloadFactory.CompletedParticipation(eventId, 1, Guid.NewGuid(), ARRIVE);
        var second = IntegrationPayloadFactory.CompletedParticipation(
            eventId,
            2,
            Guid.NewGuid(),
            ARRIVE.AddMinutes(20)
        );
        var incomplete = IntegrationPayloadFactory.ActiveParticipation(eventId, 3, Guid.NewGuid());
        var eliminated = IntegrationPayloadFactory.CompletedParticipation(
            eventId,
            4,
            Guid.NewGuid(),
            ARRIVE.AddMinutes(10),
            eliminated: true
        );
        var rides = new[] { first, second, incomplete, eliminated };
        var liveRide = IntegrationPayloadFactory.CompletedParticipation(liveId, 1, Guid.NewGuid(), ARRIVE.AddHours(29));
        var everyone = RankingOf(
            eventId,
            "Everyone",
            [
                new RankingEntry(eliminated.Id, false),
                new RankingEntry(incomplete.Id, false),
                new RankingEntry(second.Id, false),
                new RankingEntry(first.Id, false),
            ]
        );
        var withoutTheFirst = RankingOf(
            eventId,
            "Without the first",
            [new RankingEntry(first.Id, true), new RankingEntry(second.Id, false)]
        );
        var ofOne = RankingOf(eventId, "Of one", [new RankingEntry(second.Id, false)]);
        var stored = RankingOf(
            eventId,
            "Stored",
            [new RankingEntry(first.Id, false, 2), new RankingEntry(second.Id, false, 1)]
        );
        var partial = RankingOf(
            eventId,
            "Partial",
            [
                new RankingEntry(first.Id, false, 1),
                new RankingEntry(second.Id, false),
                new RankingEntry(incomplete.Id, false),
                new RankingEntry(eliminated.Id, false),
            ]
        );
        var live = RankingOf(liveId, "Live", [new RankingEntry(liveRide.Id, false)]);

        var start = LegacyWorldData.NOW.UtcDateTime.AddDays(-3);
        await Insert(
            database,
            "event_informations",
            LegacyWorldData.Information(900, "Ended ride", start, start.AddDays(1)),
            LegacyWorldData.Information(903, "Live ride", start, LegacyWorldData.NOW.UtcDateTime.AddDays(2))
        );
        await Insert(
            database,
            "event_participations",
            [.. rides.Append(liveRide).Select(x => shape.Downgrade(ParticipationModel.MapFrom(x).ToBsonDocument()))]
        );
        await Insert(
            database,
            "event_rankings",
            [
                .. new[] { everyone, withoutTheFirst, ofOne, stored, partial, live }.Select(x =>
                    shape.Downgrade(RankingModel.From(x).ToBsonDocument())
                ),
            ]
        );
        return (
            database,
            new Seeded(shape, eventId, rides)
            {
                Everyone = everyone,
                WithoutTheFirst = withoutTheFirst,
                OfOne = ofOne,
                Stored = stored,
                Partial = partial,
                Live = live,
            }
        );
    }

    static Task Insert(IMongoDatabase database, string collection, params BsonDocument[] documents)
    {
        return database.GetCollection<BsonDocument>(collection).InsertManyAsync(documents);
    }

    static Ranking RankingOf(Guid eventId, string name, IEnumerable<RankingEntry> entries)
    {
        return new Ranking(
            name,
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            eventId,
            Guid.NewGuid()
        );
    }

    sealed class Seeded
    {
        readonly Participation[] _rides;

        public Seeded(LegacyShape shape, Guid eventId, Participation[] rides)
        {
            Shape = shape;
            Event = eventId;
            _rides = rides;
        }

        public LegacyShape Shape { get; }
        public Guid Event { get; }
        public Ranking Everyone { get; init; } = default!;
        public Ranking WithoutTheFirst { get; init; } = default!;
        public Ranking OfOne { get; init; } = default!;
        public Ranking Stored { get; init; } = default!;
        public Ranking Partial { get; init; } = default!;
        public Ranking Live { get; init; } = default!;

        /// <summary>What the Results compute for the Ranking, by the number of the ride; a Ranking of one has its only entry first.</summary>
        public Dictionary<int, int?> ExpectedRanks(Ranking ranking)
        {
            var numbers = _rides.ToDictionary(x => x.Id, x => x.Combination.Number);
            var computed = new Result(ranking, _rides).Entries.ToDictionary(
                x => numbers[x.ParticipationId],
                x => x.Rank
            );
            if (ranking.Entries.Count == 1)
            {
                computed[numbers[ranking.Entries[0].ParticipationId]] = 1;
            }

            return computed;
        }
    }
}
