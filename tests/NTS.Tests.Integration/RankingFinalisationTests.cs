using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.EventData;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Ui.Storage.Core.Repositories;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// A Historic Event keeps the final placings of its Rankings (#640, ADR-0006): nothing fires at the end of the last day, so
/// the host finalises every Ranking whose Event has ended and has no stored placings, at start and then every hour, with the
/// clock it was given and in the Tenant of the Event. The finalisation is the one write the host makes to an Event that has
/// ended, by the named rule of #629, and what it stores is what the Results compute. A reader does not wait for it: until it
/// has run the placings are composed in memory. The clock is the host's and the Events end at a whole second, as in the tests
/// of #629.
/// </summary>
public sealed class RankingFinalisationTests : IClassFixture<MongoFixture>, IAsyncLifetime
{
    static readonly DateTimeOffset NOW = WholeSecond(DateTimeOffset.UtcNow);
    static readonly TimeSpan LENGTH = TimeSpan.FromHours(2);

    readonly MongoFixture _mongo;

    public RankingFinalisationTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    /// <summary>The finalisation looks at every Event of every Tenant, so each test starts from none.</summary>
    public async Task InitializeAsync()
    {
        foreach (
            var collection in new[]
            {
                "event_informations",
                "configure_events",
                "event_participations",
                "event_rankings",
            }
        )
        {
            await RegistrySeed
                .Collection(_mongo.ConnectionString, collection)
                .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        }
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task While_the_Event_is_Live_nothing_is_written()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var live = await EventAsync(tenant, endsAt: NOW + LENGTH);
        await SeedAsync(tenant, live);
        var before = await StoredRankingsAsync(live.Id);

        time.Advance(LENGTH - TimeSpan.FromSeconds(1)); // the last second of the Event
        var report = await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);

        Assert.Equal(1, report.Events);
        Assert.Equal(0, report.Finalised);
        Assert.Equal(1, report.Skipped);
        Assert.Equal(before, await StoredRankingsAsync(live.Id));
    }

    [Fact]
    public async Task Once_the_Event_has_ended_every_Ranking_of_it_gets_the_ranks_the_Results_compute()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var ended = await EventAsync(tenant, endsAt: NOW + LENGTH);
        var seeded = await SeedAsync(tenant, ended);
        var before = await StoredRankingsAsync(ended.Id);

        time.Advance(LENGTH); // the end of its last day
        var report = await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);

        Assert.Equal(3, report.Finalised);
        var after = await StoredRankingsAsync(ended.Id);
        foreach (var ranking in seeded.Rankings)
        {
            var computed = new Result(ranking, seeded.Participations).Entries.ToDictionary(
                x => x.ParticipationId,
                x => x.Rank
            );
            if (ranking.Entries.Count == 1)
            {
                computed[ranking.Entries[0].ParticipationId] = 1; // a Ranking of one is finalised like any other
            }

            Assert.Equal(computed, StoredRanks(after[ranking.Id]));
            var expected = before[ranking.Id].DeepClone().AsBsonDocument;
            foreach (var entry in expected["Entries"].AsBsonArray)
            {
                entry.AsBsonDocument["Rank"] = computed[entry.AsBsonDocument["ParticipationId"].AsGuid]!.Value;
            }

            Assert.Equal(expected, after[ranking.Id]); // nothing else of the Ranking was touched
        }
    }

    [Fact]
    public async Task The_ride_counted_in_two_Rankings_gets_the_rank_of_its_own_in_each_and_the_eliminated_and_the_not_ranked_are_numbered_as_before()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var ended = await EventAsync(tenant, endsAt: NOW + LENGTH);
        var seeded = await SeedAsync(tenant, ended);

        time.Advance(LENGTH);
        await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);

        var stored = await StoredRankingsAsync(ended.Id);
        var everyone = StoredRanks(stored[seeded.Everyone.Id]);
        var withoutTheFirst = StoredRanks(stored[seeded.WithoutTheFirst.Id]);
        Assert.Equal(1, everyone[seeded.First.Id]);
        Assert.Equal(2, everyone[seeded.Second.Id]);
        Assert.Equal(3, everyone[seeded.Incomplete.Id]);
        Assert.Equal(4, everyone[seeded.Eliminated.Id]);
        Assert.Equal(2, withoutTheFirst[seeded.First.Id]); // counted in the other Ranking, but marked not ranked here
        Assert.Equal(1, withoutTheFirst[seeded.Second.Id]);
        Assert.Equal(1, StoredRanks(stored[seeded.OfOne.Id])[seeded.Second.Id]);
    }

    [Fact]
    public async Task A_second_run_changes_nothing()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var ended = await EventAsync(tenant, endsAt: NOW + LENGTH);
        await SeedAsync(tenant, ended);
        time.Advance(LENGTH);
        var finaliser = api.Services.GetRequiredService<RankingFinaliser>();
        await finaliser.RunAsync(default);
        var once = await StoredRankingsAsync(ended.Id);

        var again = await finaliser.RunAsync(default);

        Assert.Equal(0, again.Events); // an Event whose Rankings are all final is not looked at
        Assert.Equal(0, again.Finalised);
        Assert.Equal(once, await StoredRankingsAsync(ended.Id));
    }

    [Fact]
    public async Task Until_it_has_run_a_reader_composes_the_placings_in_memory_and_afterwards_reads_the_same_ones_stored()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var ended = await EventAsync(tenant, endsAt: NOW + LENGTH);
        var seeded = await SeedAsync(tenant, ended);
        time.Advance(LENGTH);
        var client = JsonApiClients.Of(api, null, out _);
        var rankings = new RankingApiRepository(client);
        var participations = new ParticipationApiRepository(client);

        var before = await ResultsOfAsync(rankings, participations, ended.Id, seeded.Everyone.Id);
        await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);
        var stored = (await rankings.ReadMany(x => x.EventId == ended.Id)).Single(x => x.Id == seeded.Everyone.Id);
        var after = await ResultsOfAsync(rankings, participations, ended.Id, seeded.Everyone.Id);

        Assert.All(
            (await rankings.ReadMany(x => x.EventId == ended.Id)).Where(x => x.Id != seeded.Everyone.Id),
            x => Assert.True(x.IsFinal)
        );
        Assert.True(stored.IsFinal);
        Assert.Equal([1, 2, 3, 4], before.Select(x => x.Rank));
        Assert.Equal(before.Select(x => (x.ParticipationId, x.Rank)), after.Select(x => (x.ParticipationId, x.Rank)));
    }

    [Fact]
    public async Task Only_the_Events_that_have_ended_are_finalised_each_in_its_own_Tenant_and_the_rest_are_left_alone()
    {
        await using var api = NewApi(out var time);
        var bulgaria = await TenantAsync(_mongo.ConnectionString);
        var elsewhere = await TenantAsync(_mongo.ConnectionString);
        var first = await EventAsync(bulgaria, endsAt: NOW + LENGTH);
        var second = await EventAsync(elsewhere, endsAt: NOW + LENGTH);
        var later = await EventAsync(bulgaria, endsAt: NOW + LENGTH + TimeSpan.FromDays(3));
        await SeedAsync(bulgaria, first);
        await SeedAsync(elsewhere, second);
        await SeedAsync(bulgaria, later);
        var untouched = await StoredRankingsAsync(later.Id);

        time.Advance(LENGTH);
        var report = await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);

        Assert.Equal(6, report.Finalised);
        Assert.Equal(1, report.Skipped);
        Assert.All((await StoredRankingsAsync(first.Id)).Values, x => Assert.True(IsFinal(x)));
        Assert.All((await StoredRankingsAsync(second.Id)).Values, x => Assert.True(IsFinal(x)));
        Assert.Equal(untouched, await StoredRankingsAsync(later.Id));
        Assert.All((await StoredRankingsAsync(second.Id)).Values, x => Assert.Equal(elsewhere, x["TenantId"].AsString));
        Assert.All((await StoredRankingsAsync(first.Id)).Values, x => Assert.Equal(bulgaria, x["TenantId"].AsString));
    }

    [Fact]
    public async Task A_Ranking_with_only_some_ranks_stored_is_reported_and_left_alone_and_the_others_are_finalised()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var ended = await EventAsync(tenant, endsAt: NOW + LENGTH);
        var seeded = await SeedAsync(tenant, ended);
        var partial = new Ranking(
            "Half done",
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            [new RankingEntry(seeded.First.Id, false, 1), new RankingEntry(seeded.Second.Id, false)],
            ended.Id,
            Guid.NewGuid()
        );
        await EventSeed.RankingAsync(_mongo.ConnectionString, tenant, partial);
        var before = (await StoredRankingsAsync(ended.Id))[partial.Id];

        time.Advance(LENGTH);
        var report = await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);

        Assert.Equal(3, report.Finalised);
        Assert.Equal([partial.Id], report.SomePlacingsStored);
        Assert.Equal(before, (await StoredRankingsAsync(ended.Id))[partial.Id]);
    }

    [Fact]
    public async Task A_Ranking_whose_Event_is_not_there_is_skipped_and_is_no_error()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var orphan = IntegrationPayloadFactory.CompletedParticipation(Guid.NewGuid(), 1, Guid.NewGuid(), NOW);
        var ranking = new Ranking(
            "Orphan",
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            [new RankingEntry(orphan.Id, false)],
            orphan.EventId,
            Guid.NewGuid()
        );
        await EventSeed.RankingAsync(_mongo.ConnectionString, tenant, ranking);
        var before = await StoredRankingsAsync(orphan.EventId);
        time.Advance(LENGTH);

        var report = await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);

        Assert.Equal(1, report.Events);
        Assert.Equal(0, report.Finalised);
        Assert.Equal(1, report.Skipped);
        Assert.Equal(before, await StoredRankingsAsync(orphan.EventId));
    }

    [Fact]
    public async Task Finalisers_that_meet_in_the_database_write_each_Ranking_once()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var ended = await EventAsync(tenant, endsAt: NOW + LENGTH);
        await SeedAsync(tenant, ended);
        time.Advance(LENGTH);
        var finaliser = api.Services.GetRequiredService<RankingFinaliser>();

        var reports = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => finaliser.RunAsync(default)));

        Assert.Equal(3, reports.Sum(x => x.Finalised));
        Assert.All((await StoredRankingsAsync(ended.Id)).Values, x => Assert.True(IsFinal(x)));
    }

    [Fact]
    public async Task Every_Participation_of_an_Event_is_read_however_many_it_has()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var ended = await EventAsync(tenant, endsAt: NOW + LENGTH);
        var arrive = NOW.AddHours(-1);
        var participations = Enumerable
            .Range(1, 1001)
            .Select(x =>
                IntegrationPayloadFactory.CompletedParticipation(ended.Id, x, Guid.NewGuid(), arrive.AddSeconds(x))
            )
            .ToList();
        ApiMongo.Configure();
        await RegistrySeed
            .Collection(_mongo.ConnectionString, "event_participations")
            .InsertManyAsync(
                participations.Select(x =>
                {
                    var model = ParticipationModel.MapFrom(x);
                    model.TenantId = tenant;
                    return model.ToBsonDocument();
                })
            );
        var everyone = RankingOf(ended.Id, "Everyone", participations.Select(x => new RankingEntry(x.Id, false)));
        await EventSeed.RankingAsync(_mongo.ConnectionString, tenant, everyone);

        time.Advance(LENGTH);
        var report = await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);

        Assert.Equal(1, report.Finalised);
        Assert.Empty(report.Refused);
        var ranks = StoredRanks((await StoredRankingsAsync(ended.Id))[everyone.Id]);
        Assert.Equal(Enumerable.Range(1, 1001), ranks.Values.Select(x => x!.Value).Order());
        Assert.Equal(1, ranks[participations[0].Id]); // the first to arrive
    }

    [Fact]
    public async Task A_Ranking_that_counts_a_Participation_that_is_not_there_is_reported_and_left_alone_and_the_others_are_finalised()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var ended = await EventAsync(tenant, endsAt: NOW + LENGTH);
        var seeded = await SeedAsync(tenant, ended);
        var stale = RankingOf(
            ended.Id,
            "Stale",
            [new RankingEntry(seeded.First.Id, false), new RankingEntry(Guid.NewGuid(), false)]
        );
        await EventSeed.RankingAsync(_mongo.ConnectionString, tenant, stale);
        var before = (await StoredRankingsAsync(ended.Id))[stale.Id];

        time.Advance(LENGTH);
        var report = await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);

        Assert.Equal(3, report.Finalised);
        Assert.Equal([stale.Id], report.Refused);
        Assert.Empty(report.Failed);
        Assert.Equal(before, (await StoredRankingsAsync(ended.Id))[stale.Id]);
    }

    [Fact]
    public async Task A_Ranking_that_counts_a_Participation_twice_is_reported_and_left_alone_and_the_others_are_finalised()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var ended = await EventAsync(tenant, endsAt: NOW + LENGTH);
        var seeded = await SeedAsync(tenant, ended);
        var twice = RankingOf(ended.Id, "Twice", [new RankingEntry(seeded.First.Id, false)]);
        await EventSeed.RankingAsync(_mongo.ConnectionString, tenant, twice);
        var rankings = RegistrySeed.Collection(_mongo.ConnectionString, "event_rankings");
        var id = new BsonDocument("_id", RegistrySeed.Binary(twice.Id));
        var stored = await rankings.Find(id).SingleAsync();
        await rankings.UpdateOneAsync(id, Builders<BsonDocument>.Update.Push("Entries", stored["Entries"][0]));
        var before = (await StoredRankingsAsync(ended.Id))[twice.Id];

        time.Advance(LENGTH);
        var report = await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);

        Assert.Equal(3, report.Finalised);
        Assert.Equal([twice.Id], report.Refused);
        Assert.Empty(report.Failed);
        Assert.Equal(before, (await StoredRankingsAsync(ended.Id))[twice.Id]);
    }

    [Fact]
    public async Task An_Event_whose_documents_cannot_be_read_is_reported_and_does_not_keep_the_others_from_their_placings()
    {
        await using var api = NewApi(out var time);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var healthy = new List<EventHandle>();
        for (var i = 0; i < 3; i++)
        {
            var ended = await EventAsync(tenant, endsAt: NOW + LENGTH);
            await SeedAsync(tenant, ended);
            healthy.Add(ended);
        }

        var broken = await EventAsync(tenant, endsAt: NOW + LENGTH);
        await SeedAsync(tenant, broken);
        await RegistrySeed
            .Collection(_mongo.ConnectionString, "event_participations")
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", RegistrySeed.Binary(Guid.NewGuid()) },
                    { "TenantId", tenant },
                    { "EventId", RegistrySeed.Binary(broken.Id) },
                    { "Version", "not a number" },
                }
            );
        var before = await StoredRankingsAsync(broken.Id);

        time.Advance(LENGTH);
        var report = await api.Services.GetRequiredService<RankingFinaliser>().RunAsync(default);

        Assert.Equal([broken.Id], report.Failed);
        Assert.Equal(9, report.Finalised);
        foreach (var ended in healthy)
        {
            Assert.All((await StoredRankingsAsync(ended.Id)).Values, x => Assert.True(IsFinal(x)));
        }

        Assert.Equal(before, await StoredRankingsAsync(broken.Id));
    }

    [Fact]
    public async Task The_host_finalises_at_start_and_then_every_hour_by_the_clock_it_was_given()
    {
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var atStart = await EventAsync(tenant, endsAt: NOW - TimeSpan.FromDays(1));
        await SeedAsync(tenant, atStart);
        var time = new FakeTimeProvider(NOW);
        await using var api = new ApiFactory(_mongo.ConnectionString, time: time, finaliseRankings: true);
        _ = api.Services; // the host starts

        await Eventually(async () => (await StoredRankingsAsync(atStart.Id)).Values.All(IsFinal));
        var afterStart = await EventAsync(tenant, endsAt: NOW - TimeSpan.FromHours(1));
        await SeedAsync(tenant, afterStart);
        time.Advance(TimeSpan.FromMinutes(59));
        await Task.Delay(500);
        var beforeTheHour = await StoredRankingsAsync(afterStart.Id);
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.All(beforeTheHour.Values, x => Assert.False(IsFinal(x)));
        await Eventually(async () => (await StoredRankingsAsync(afterStart.Id)).Values.All(IsFinal));
        var afterTheFirstHour = await EventAsync(tenant, endsAt: NOW);
        await SeedAsync(tenant, afterTheFirstHour);
        time.Advance(TimeSpan.FromHours(1));
        await Eventually(async () => (await StoredRankingsAsync(afterTheFirstHour.Id)).Values.All(IsFinal));
    }

    [Fact]
    public async Task A_run_that_fails_is_logged_and_tried_again_an_hour_later_and_the_host_keeps_running()
    {
        var logs = new LogCapture();
        var time = new FakeTimeProvider(NOW);
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            time: time,
            finaliseRankings: true,
            configureServices: services =>
            {
                services.AddSingleton<ILoggerProvider>(logs);
                // the host works on the database the test gave it, and the finalisation on one that nothing listens to
                services.AddSingleton(sp => new RankingFinaliser(
                    new CrossTenantReads(
                        new MongoClient("mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=300&connectTimeoutMS=300"),
                        Options.Create(new NIdentityOptions { Database = UserSeed.DATABASE })
                    ),
                    sp.GetRequiredService<EventDataAccess>(),
                    sp.GetRequiredService<ILogger<RankingFinaliser>>()
                ));
            }
        );
        var lifetime = api.Services.GetRequiredService<IHostApplicationLifetime>(); // the host starts

        await Eventually(() => Task.FromResult(Failures(logs) == 1), "The failed finalisation was not logged.");
        time.Advance(TimeSpan.FromHours(1));
        await Eventually(() => Task.FromResult(Failures(logs) == 2), "The finalisation was not tried again.");

        Assert.False(lifetime.ApplicationStopping.IsCancellationRequested);
    }

    [Fact]
    public async Task A_host_that_is_told_not_to_finalise_does_not()
    {
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var ended = await EventAsync(tenant, endsAt: NOW - TimeSpan.FromDays(1));
        await SeedAsync(tenant, ended);
        var before = await StoredRankingsAsync(ended.Id);
        await using var api = NewApi(out var time); // the factory turns it off unless a test turns it on
        _ = api.Services;

        time.Advance(TimeSpan.FromHours(3));
        await Task.Delay(500);

        Assert.Equal(before, await StoredRankingsAsync(ended.Id));
    }

    ApiFactory NewApi(out FakeTimeProvider time)
    {
        time = new FakeTimeProvider(NOW);
        return new ApiFactory(_mongo.ConnectionString, time: time);
    }

    /// <summary>An Event that has started and whose last day ends at the instant given.</summary>
    async Task<EventHandle> EventAsync(string tenant, DateTimeOffset endsAt)
    {
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);
        await EventSeed.StartAsync(_mongo.ConnectionString, id, tenant, null, endsAt);
        return new EventHandle(id, tenant);
    }

    /// <summary>
    /// Four Participations (one arrived first, one second, one that has not finished, one that was eliminated) and three
    /// Rankings with no ranks stored: one of everyone, one in which the first is marked not ranked, and one of the second alone.
    /// </summary>
    async Task<Seeded> SeedAsync(string tenant, EventHandle ended)
    {
        var arrive = NOW.AddHours(-1);
        var first = IntegrationPayloadFactory.CompletedParticipation(ended.Id, 1, Guid.NewGuid(), arrive);
        var second = IntegrationPayloadFactory.CompletedParticipation(
            ended.Id,
            2,
            Guid.NewGuid(),
            arrive.AddMinutes(20)
        );
        var incomplete = IntegrationPayloadFactory.ActiveParticipation(ended.Id, 3, Guid.NewGuid());
        var eliminated = IntegrationPayloadFactory.CompletedParticipation(
            ended.Id,
            4,
            Guid.NewGuid(),
            arrive.AddMinutes(10),
            eliminated: true
        );
        Participation[] participations = [first, second, incomplete, eliminated];
        foreach (var participation in participations)
        {
            await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        }

        var everyone = RankingOf(
            ended.Id,
            "Everyone",
            [
                new RankingEntry(eliminated.Id, false),
                new RankingEntry(incomplete.Id, false),
                new RankingEntry(second.Id, false),
                new RankingEntry(first.Id, false),
            ]
        );
        var withoutTheFirst = RankingOf(
            ended.Id,
            "Without the first",
            [new RankingEntry(first.Id, true), new RankingEntry(second.Id, false)]
        );
        var ofOne = RankingOf(ended.Id, "Of one", [new RankingEntry(second.Id, false)]);
        foreach (var ranking in new[] { everyone, withoutTheFirst, ofOne })
        {
            await EventSeed.RankingAsync(_mongo.ConnectionString, tenant, ranking);
        }

        return new Seeded(participations, [everyone, withoutTheFirst, ofOne], first, second, incomplete, eliminated)
        {
            Everyone = everyone,
            WithoutTheFirst = withoutTheFirst,
            OfOne = ofOne,
        };
    }

    async Task<Dictionary<Guid, BsonDocument>> StoredRankingsAsync(Guid eventId)
    {
        var all = await RegistrySeed
            .Collection(_mongo.ConnectionString, "event_rankings")
            .Find(new BsonDocument("EventId", RegistrySeed.Binary(eventId)))
            .ToListAsync();
        return all.ToDictionary(x => x["_id"].AsGuid);
    }

    static async Task<IReadOnlyList<NTS.Domain.Core.Aggregates.Results.ParticipationResult>> ResultsOfAsync(
        RankingApiRepository rankings,
        ParticipationApiRepository participations,
        Guid eventId,
        Guid rankingId
    )
    {
        var ranking = (await rankings.ReadMany(x => x.EventId == eventId)).Single(x => x.Id == rankingId);
        var all = await participations.ReadMany(x => x.EventId == eventId);
        return new Result(ranking, all).Entries;
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

    static Dictionary<Guid, int?> StoredRanks(BsonDocument ranking)
    {
        return ranking["Entries"]
            .AsBsonArray.Select(x => x.AsBsonDocument)
            .ToDictionary(
                x => x["ParticipationId"].AsGuid,
                x => x.TryGetValue("Rank", out var rank) && !rank.IsBsonNull ? (int?)rank.ToInt32() : null
            );
    }

    static bool IsFinal(BsonDocument ranking)
    {
        return StoredRanks(ranking).Values.All(x => x != null);
    }

    static DateTimeOffset WholeSecond(DateTimeOffset value)
    {
        return new DateTimeOffset(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Offset);
    }

    static int Failures(LogCapture logs)
    {
        return logs.Entries.Count(x =>
            x.Level == LogLevel.Error && x.Category == typeof(RankingFinalisationSweep).FullName
        );
    }

    static async Task Eventually(
        Func<Task<bool>> condition,
        string failure = "The host did not finalise the Rankings in time."
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.True(await condition(), failure);
    }

    sealed class EventHandle
    {
        public EventHandle(Guid id, string tenant)
        {
            Id = id;
            Tenant = tenant;
        }

        public Guid Id { get; }
        public string Tenant { get; }
    }

    sealed class Seeded
    {
        public Seeded(
            Participation[] participations,
            Ranking[] rankings,
            Participation first,
            Participation second,
            Participation incomplete,
            Participation eliminated
        )
        {
            Participations = participations;
            Rankings = rankings;
            First = first;
            Second = second;
            Incomplete = incomplete;
            Eliminated = eliminated;
        }

        public Participation[] Participations { get; }
        public Ranking[] Rankings { get; }
        public Participation First { get; }
        public Participation Second { get; }
        public Participation Incomplete { get; }
        public Participation Eliminated { get; }
        public Ranking Everyone { get; init; } = default!;
        public Ranking WithoutTheFirst { get; init; } = default!;
        public Ranking OfOne { get; init; } = default!;
    }
}
