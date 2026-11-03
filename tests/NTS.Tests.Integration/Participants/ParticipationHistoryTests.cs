using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The history of an Athlete or a Horse, <c>/api/participations/history</c> (#638, ADR-0010, ADR-0006, the rest-api skill): one
/// row for each ride of an Event that has ended, in any Tenant, newest first, with the placing the ride keeps in the Ranking of
/// its own Competition and Category and the other Rankings that count it. It is for signed-in accounts alone, and the public
/// Participations resource refuses an anonymous filter on the Athlete or the Horse. Over the real Api in this process, with the
/// rides and the Rankings stored the way the Api keeps them.
/// </summary>
public sealed class ParticipationHistoryTests : IClassFixture<SharedApiFixture>
{
    // Every test has Participants of its own: the database is shared by all of them, and a Participant is found by its id.
    readonly Participant _ana = new(Guid.NewGuid(), "Мариела Тестова", "Mariela Testova");
    readonly Participant _boris = new(Guid.NewGuid(), "Борис Примеров", "Boris Primerov");
    readonly Participant _fire = new(Guid.NewGuid(), "Огън", "Fire");
    readonly Participant _wind = new(Guid.NewGuid(), "Вятър", "Wind");
    readonly SharedApiFixture _shared;

    public ParticipationHistoryTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task A_caller_who_is_not_signed_in_is_refused_and_a_signed_in_one_reads_the_rides_of_an_Athlete_and_of_a_Horse()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Spring ride", "Plovdiv", 30);
        var ride = HistorySeed.Ride(past, 1, _ana, _fire);
        await StoreAsync(tenant, ride, Ranked(past, "CEI 1*", (ride, 1)));

        using var anonymous = await client.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}");
        var ofTheAthlete = await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}");
        var ofTheHorse = await me.Page.GetAsync($"/api/participations/history?filter=horseId eq {_fire.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("not-signed-in", await ErrorCodeAsync(anonymous));
        Assert.Equal(HttpStatusCode.OK, ofTheAthlete.StatusCode);
        Assert.Equal("application/vnd.api+json", ofTheAthlete.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Accept-Language", ofTheAthlete.Headers.Vary); // the names are told in the language of the request
        var athletes = (await ApiSessions.ReadJsonAsync(ofTheAthlete)).GetProperty("data");
        var row = Assert.Single(athletes.EnumerateArray());
        Assert.Equal("participation-histories", row.GetProperty("type").GetString());
        Assert.Equal(ride.Id.ToString(), row.GetProperty("id").GetString());
        Assert.Equal($"/api/participations/{ride.Id}", row.GetProperty("links").GetProperty("self").GetString()); // the Participation itself is the public one
        var attributes = row.GetProperty("attributes");
        Assert.Equal(past.ToString(), attributes.GetProperty("eventId").GetString());
        Assert.Equal("Spring ride", attributes.GetProperty("eventName").GetString());
        Assert.Equal("Plovdiv", attributes.GetProperty("location").GetString());
        Assert.True(
            attributes.GetProperty("startDay").GetDateTimeOffset()
                < attributes.GetProperty("endDay").GetDateTimeOffset()
        );
        Assert.True(attributes.GetProperty("endDay").GetDateTimeOffset() < DateTimeOffset.UtcNow);
        Assert.Equal("FEI", attributes.GetProperty("ruleset").GetString());
        Assert.Equal("CEI 1*", attributes.GetProperty("competition").GetString());
        Assert.Equal("Senior", attributes.GetProperty("category").GetString());
        Assert.Equal(_ana.Id.ToString(), attributes.GetProperty("athleteId").GetString());
        Assert.Equal(_fire.Id.ToString(), attributes.GetProperty("horseId").GetString());
        var other = attributes.GetProperty("other"); // the other half of the Combination
        Assert.Equal("Horse", other.GetProperty("kind").GetString());
        Assert.Equal(_fire.Id.ToString(), other.GetProperty("id").GetString());
        Assert.Equal("Fire", other.GetProperty("displayName").GetString());
        var horses = (await ApiSessions.ReadJsonAsync(ofTheHorse)).GetProperty("data");
        var horseRow = Assert.Single(horses.EnumerateArray());
        Assert.Equal(ride.Id.ToString(), horseRow.GetProperty("id").GetString());
        var inTheHorsesList = horseRow.GetProperty("attributes").GetProperty("other");
        Assert.Equal("Athlete", inTheHorsesList.GetProperty("kind").GetString());
        Assert.Equal(_ana.Id.ToString(), inTheHorsesList.GetProperty("id").GetString());
        Assert.Equal("Mariela Testova", inTheHorsesList.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Only_the_rides_of_Events_that_have_ended_are_listed_newest_first_in_any_Tenant_and_a_deleted_ride_is_not_one()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var other = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var oldest = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Autumn ride",
            "Varna",
            400
        );
        var newest = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, other, "Spring ride", "Sofia", 10);
        var middle = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Winter ride",
            "Plovdiv",
            200
        );
        var live = await HistorySeed.LiveAsync(_shared.MongoConnectionString, tenant, "Summer ride", "Burgas");
        var deleted = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Deleted ride",
            "Ruse",
            100
        );
        var rides = new Dictionary<string, Participation>
        {
            ["oldest"] = HistorySeed.Ride(oldest, 1, _ana, _fire),
            ["newest"] = HistorySeed.Ride(newest, 1, _ana, _wind),
            ["middle"] = HistorySeed.Ride(middle, 1, _ana, _fire),
            ["live"] = HistorySeed.Ride(live, 1, _ana, _fire),
            ["deleted"] = HistorySeed.Ride(deleted, 1, _ana, _fire),
            ["somebody else"] = HistorySeed.Ride(middle, 2, _boris, _wind),
        };
        await StoreAsync(tenant, rides["oldest"], Ranked(oldest, "CEI 1*", (rides["oldest"], 1)));
        await StoreAsync(other, rides["newest"], Ranked(newest, "CEI 1*", (rides["newest"], 1)));
        await StoreAsync(tenant, rides["middle"], Ranked(middle, "CEI 1*", (rides["middle"], 1)));
        await StoreAsync(tenant, rides["live"], Ranked(live, "CEI 1*", (rides["live"], 1)));
        await StoreAsync(tenant, rides["deleted"], Ranked(deleted, "CEI 1*", (rides["deleted"], 1)));
        await EventSeed.ParticipationAsync(_shared.MongoConnectionString, tenant, rides["somebody else"]);
        await HistorySeed.SoftDeleteAsync(_shared.MongoConnectionString, rides["deleted"].Id);

        var response = await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}");

        var listed = await RowsAsync(response);
        Assert.Equal(
            new[] { rides["newest"].Id, rides["middle"].Id, rides["oldest"].Id }.Select(x => x.ToString()),
            listed.Select(x => x.GetProperty("id").GetString())
        );
        Assert.Equal(
            ["Sofia", "Plovdiv", "Varna"],
            listed.Select(x => x.GetProperty("attributes").GetProperty("location").GetString())
        );
    }

    [Fact]
    public async Task A_ride_of_an_Event_that_was_deleted_is_not_listed()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var kept = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Kept ride", "Plovdiv", 30);
        var gone = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Deleted ride", "Varna", 60);
        var keptRide = HistorySeed.Ride(kept, 1, _ana, _fire);
        var goneRide = HistorySeed.Ride(gone, 1, _ana, _fire);
        await StoreAsync(
            tenant,
            keptRide,
            Ranked(kept, "CEI 1*", (keptRide, 1)),
            goneRide,
            Ranked(gone, "CEI 1*", (goneRide, 1))
        );
        await HistorySeed.SoftDeleteEventAsync(_shared.MongoConnectionString, gone);

        var response = await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}");

        Assert.Equal(
            [keptRide.Id.ToString()],
            (await RowsAsync(response)).Select(x => x.GetProperty("id").GetString())
        );
    }

    [Theory]
    [InlineData(false)] // the ride was deleted
    [InlineData(true)] // the Event was deleted
    public async Task What_was_deleted_takes_no_place_on_a_page_and_makes_no_page_after_it(bool theEvent)
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var spring = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Spring ride", "Sofia", 10);
        var goneFirst = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Deleted ride",
            "Varna",
            30
        );
        var autumn = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Autumn ride", "Ruse", 50);
        var goneLast = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Deleted again",
            "Burgas",
            70
        );
        var springRide = HistorySeed.Ride(spring, 1, _ana, _fire);
        var firstGone = HistorySeed.Ride(goneFirst, 1, _ana, _fire);
        var autumnRide = HistorySeed.Ride(autumn, 1, _ana, _fire);
        var lastGone = HistorySeed.Ride(goneLast, 1, _ana, _fire);
        await StoreAsync(tenant, springRide, firstGone, autumnRide, lastGone);
        if (theEvent)
        {
            await HistorySeed.SoftDeleteEventAsync(_shared.MongoConnectionString, goneFirst);
            await HistorySeed.SoftDeleteEventAsync(_shared.MongoConnectionString, goneLast);
        }
        else
        {
            await HistorySeed.SoftDeleteAsync(_shared.MongoConnectionString, firstGone.Id);
            await HistorySeed.SoftDeleteAsync(_shared.MongoConnectionString, lastGone.Id);
        }

        var response = await me.Page.GetAsync(
            $"/api/participations/history?filter=athleteId eq {_ana.Id}&page[size]=2"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiSessions.ReadJsonAsync(response);
        Assert.Equal(
            new[] { springRide.Id, autumnRide.Id }.Select(x => x.ToString()), // the page is full: the one between them is not counted
            body.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetString())
        );
        Assert.False(body.GetProperty("links").TryGetProperty("next", out _)); // and the one after them is no page of its own
    }

    [Fact]
    public async Task An_Event_is_over_from_the_second_its_last_day_ends_and_not_before()
    {
        var end = new DateTimeOffset(2030, 5, 21, 23, 59, 59, TimeSpan.Zero);
        var time = new FakeTimeProvider(end.AddSeconds(-1));
        await using var api = new ApiFactory(_shared.MongoConnectionString, time: time);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var edge = await HistorySeed.EventAsync(
            _shared.MongoConnectionString,
            tenant,
            "Edge ride",
            "Plovdiv",
            end.AddHours(-23),
            end
        );
        var ride = HistorySeed.Ride(edge, 1, _ana, _fire);
        await StoreAsync(tenant, ride, Ranked(edge, "CEI 1*", (ride, 1)));
        var path = $"/api/participations/history?filter=athleteId eq {_ana.Id}";

        var before = await RowsAsync(await me.Page.GetAsync(path));
        time.SetUtcNow(end);
        var at = await RowsAsync(await me.Page.GetAsync(path));

        Assert.Empty(before); // the last second is still the Event's
        Assert.Equal([ride.Id.ToString()], at.Select(x => x.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task A_page_reads_only_the_rides_the_Events_and_the_Rankings_that_are_on_it()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var oldest = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Autumn ride",
            "Varna",
            300
        );
        var middle = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Winter ride",
            "Plovdiv",
            200
        );
        var newest = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Spring ride",
            "Sofia",
            100
        );
        var oldestRide = HistorySeed.Ride(oldest, 1, _ana, _fire);
        var middleRide = HistorySeed.Ride(middle, 1, _ana, _fire);
        var newestRide = HistorySeed.Ride(newest, 1, _ana, _fire);
        var oldestRanking = Ranked(oldest, "CEI 1*", (oldestRide, 1));
        await StoreAsync(
            tenant,
            oldestRide,
            oldestRanking,
            middleRide,
            Ranked(middle, "CEI 1*", (middleRide, 1)),
            newestRide,
            Ranked(newest, "CEI 1*", (newestRide, 1))
        );
        await HistorySeed.SpoilParticipationAsync(_shared.MongoConnectionString, oldestRide.Id); // something that cannot be read whole
        await HistorySeed.SpoilRankingAsync(_shared.MongoConnectionString, oldestRanking.Id);

        var response = await me.Page.GetAsync(
            $"/api/participations/history?filter=athleteId eq {_ana.Id}&page[size]=2"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // the oldest ride is on the next page, and nothing of it is read for this one
        var body = await ApiSessions.ReadJsonAsync(response);
        var page = body.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(
            new[] { newestRide.Id, middleRide.Id }.Select(x => x.ToString()),
            page.Select(x => x.GetProperty("id").GetString())
        );
        Assert.Equal(1, page[0].GetProperty("attributes").GetProperty("placing").GetProperty("rank").GetInt32());
        Assert.Equal(JsonValueKind.String, body.GetProperty("links").GetProperty("next").ValueKind);
    }

    [Fact]
    public async Task The_placing_is_the_stored_rank_in_the_Ranking_of_the_rides_own_Competition_and_Category_and_a_ride_counted_in_two_Rankings_lists_the_other()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var ranked = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Ranked ride",
            "Plovdiv",
            30
        );
        var eliminated = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Eliminated ride",
            "Varna",
            60
        );
        var notRanked = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Not ranked ride",
            "Sofia",
            90
        );
        // The ride is a Junior's of "CEI 2*", and each Ranking that looks like its own Ranking differs from it in one thing. They
        // all come before it in the order the reader looks in (by name, Category and id) and give the ride another rank, so that a
        // reader that took one of them for the ride's own would show another number.
        var mine = HistorySeed.Ride(
            ranked,
            1,
            _ana,
            _fire,
            "CEI 2*",
            category: ParticipationCategory.JuniorOrYoungAdult
        );
        var leader = HistorySeed.Ride(
            ranked,
            2,
            _boris,
            _wind,
            "CEI 2*",
            category: ParticipationCategory.JuniorOrYoungAdult
        );
        var last = HistorySeed.Ride(
            ranked,
            3,
            new Participant(Guid.NewGuid(), "Трети"),
            new Participant(Guid.NewGuid(), "Кон"),
            "CEI 2*",
            category: ParticipationCategory.JuniorOrYoungAdult
        );
        var withdrawn = HistorySeed.Ride(eliminated, 1, _ana, _fire, eliminated: new Withdrawn());
        var winner = HistorySeed.Ride(eliminated, 2, _boris, _wind);
        var counted = HistorySeed.Ride(notRanked, 1, _ana, _fire);
        var champion = HistorySeed.Ride(notRanked, 2, _boris, _wind);
        await StoreAsync(tenant, mine, leader, last);
        await StoreAsync(tenant, withdrawn, winner);
        await StoreAsync(tenant, counted, champion);
        var junior = ParticipationCategory.JuniorOrYoungAdult;
        var own = HistorySeed.RankingWithId(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            ranked,
            "CEI 2*",
            CompetitionRuleset.FEI,
            junior,
            new RankingEntry(leader.Id, false, 1),
            new RankingEntry(mine.Id, false, 2),
            new RankingEntry(last.Id, false, 3)
        );
        var anotherName = HistorySeed.Ranking(ranked, "CEI 1*", CompetitionRuleset.FEI, junior, Entry(mine, 3));
        var anotherRuleset = HistorySeed.RankingWithId(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ranked,
            "CEI 2*",
            CompetitionRuleset.Regional,
            junior,
            Entry(mine, 4)
        );
        var anotherCategory = HistorySeed.Ranking(
            ranked,
            "CEI 2*",
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            Entry(mine, 5)
        );
        var custom = HistorySeed.RankingWithId(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            ranked,
            "National Championship",
            CompetitionRuleset.Regional,
            ParticipationCategory.Senior,
            new RankingEntry(mine.Id, false, 1),
            new RankingEntry(leader.Id, true, 2)
        );
        var twin = HistorySeed.RankingWithId(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            ranked,
            "National Championship", // the same name and category as the one before, stored after it, with an id that comes first
            CompetitionRuleset.Regional,
            ParticipationCategory.Senior,
            Entry(mine, 6)
        );
        var unrelated = HistorySeed.Ranking(
            ranked,
            "CEI 4*", // a Ranking of the Event that counts other rides than this one
            CompetitionRuleset.FEI,
            junior,
            Entry(leader, 1),
            Entry(last, 2)
        );
        foreach (var ranking in new[] { own, anotherName, anotherRuleset, anotherCategory, custom, twin, unrelated })
        {
            await EventSeed.RankingAsync(_shared.MongoConnectionString, tenant, ranking);
        }

        await EventSeed.RankingAsync(
            _shared.MongoConnectionString,
            tenant,
            Ranked(eliminated, "CEI 1*", (winner, 1), (withdrawn, 2))
        );
        await EventSeed.RankingAsync(
            _shared.MongoConnectionString,
            tenant,
            HistorySeed.Ranking(
                notRanked,
                "CEI 1*",
                CompetitionRuleset.FEI,
                ParticipationCategory.Senior,
                new RankingEntry(champion.Id, false, 1),
                new RankingEntry(counted.Id, true, 2)
            )
        );

        var rows = await RowsAsync(
            await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}")
        );

        var byEvent = rows.ToDictionary(x => x.GetProperty("attributes").GetProperty("eventId").GetString()!);
        var rankedRow = byEvent[ranked.ToString()].GetProperty("attributes");
        Assert.Equal("Ranked", rankedRow.GetProperty("placing").GetProperty("kind").GetString());
        Assert.Equal(2, rankedRow.GetProperty("placing").GetProperty("rank").GetInt32()); // the rank its own Ranking stores, whichever the others give
        var also = rankedRow.GetProperty("alsoCountedIn").EnumerateArray().ToList();
        Assert.Equal(
            ["CEI 1*", "CEI 2*", "CEI 2*", "National Championship", "National Championship"],
            also.Select(x => x.GetProperty("name").GetString())
        ); // by name, then category: the ride's own Ranking is not "also"
        Assert.Equal(
            new[] { anotherName.Id, anotherCategory.Id, anotherRuleset.Id, twin.Id, custom.Id }.Select(x =>
                x.ToString()
            ),
            also.Select(x => x.GetProperty("rankingId").GetString())
        );
        Assert.Equal(
            ["JuniorOrYoungAdult", "Senior", "JuniorOrYoungAdult", "Senior", "Senior"],
            also.Select(x => x.GetProperty("category").GetString())
        );
        Assert.Equal(
            ["FEI", "FEI", "Regional", "Regional", "Regional"],
            also.Select(x => x.GetProperty("ruleset").GetString())
        );
        Assert.Equal(
            [3, 5, 4, 6, 1],
            also.Select(x => x.GetProperty("placing").GetProperty("rank").GetInt32()) // the rank the ride holds in each
        );
        var eliminatedRow = byEvent[eliminated.ToString()].GetProperty("attributes");
        Assert.Equal("Eliminated", eliminatedRow.GetProperty("placing").GetProperty("kind").GetString());
        Assert.Equal("WD", eliminatedRow.GetProperty("placing").GetProperty("code").GetString());
        Assert.False(eliminatedRow.GetProperty("placing").TryGetProperty("rank", out _));
        Assert.Empty(eliminatedRow.GetProperty("alsoCountedIn").EnumerateArray());
        var notRankedRow = byEvent[notRanked.ToString()].GetProperty("attributes");
        Assert.Equal("NotRanked", notRankedRow.GetProperty("placing").GetProperty("kind").GetString());
        Assert.False(notRankedRow.GetProperty("placing").TryGetProperty("rank", out _));
    }

    [Fact]
    public async Task A_Ranking_that_holds_no_ranks_yet_is_ranked_in_memory_and_its_only_entry_is_rank_1()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var shared = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Shared ride",
            "Plovdiv",
            30
        );
        var alone = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Lonely ride", "Varna", 60);
        var arrive = DateTimeOffset.UtcNow.AddHours(-5);
        var quick = HistorySeed.Ride(shared, 1, _boris, _wind, arrive: arrive);
        var slow = HistorySeed.Ride(shared, 2, _ana, _fire, arrive: arrive.AddMinutes(40));
        var single = HistorySeed.Ride(alone, 1, _ana, _fire);
        await StoreAsync(tenant, quick, slow);
        await StoreAsync(tenant, single);
        await EventSeed.RankingAsync(
            _shared.MongoConnectionString,
            tenant,
            HistorySeed.Ranking(
                shared,
                "CEI 1*",
                CompetitionRuleset.FEI,
                ParticipationCategory.Senior,
                new RankingEntry(slow.Id, false),
                new RankingEntry(quick.Id, false)
            ) // listed the other way round, with nothing stored: the times rank them
        );
        await EventSeed.RankingAsync(
            _shared.MongoConnectionString,
            tenant,
            HistorySeed.Ranking(
                alone,
                "CEI 1*",
                CompetitionRuleset.FEI,
                ParticipationCategory.Senior,
                new RankingEntry(single.Id, false)
            )
        );

        var rows = await RowsAsync(
            await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}")
        );

        var placings = rows.ToDictionary(
            x => x.GetProperty("attributes").GetProperty("eventId").GetString()!,
            x => x.GetProperty("attributes").GetProperty("placing").GetProperty("rank").GetInt32()
        );
        Assert.Equal(2, placings[shared.ToString()]);
        Assert.Equal(1, placings[alone.ToString()]); // the Results leave one entry unranked; the history says 1
    }

    [Fact]
    public async Task A_Ranking_that_cannot_be_ranked_leaves_the_placing_out_and_the_ride_is_listed_all_the_same()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var broken = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Broken ride",
            "Plovdiv",
            30
        );
        var fine = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Fine ride", "Varna", 60);
        var unreadable = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Unreadable ride",
            "Sofia",
            90
        );
        var mine = HistorySeed.Ride(broken, 1, _ana, _fire);
        var gone = HistorySeed.Ride(broken, 2, _boris, _wind); // counted by the Ranking and not stored
        var good = HistorySeed.Ride(fine, 1, _ana, _fire);
        var spoiled = HistorySeed.Ride(unreadable, 1, _ana, _fire); // stored with a blank name, which the domain does not accept
        await StoreAsync(tenant, mine);
        await StoreAsync(tenant, good, Ranked(fine, "CEI 1*", (good, 1)));
        await StoreAsync(tenant, spoiled, Ranked(unreadable, "CEI 1*", (spoiled, 1)));
        await HistorySeed.BlankTheAthleteNameAsync(_shared.MongoConnectionString, spoiled.Id);
        await EventSeed.RankingAsync(
            _shared.MongoConnectionString,
            tenant,
            HistorySeed.Ranking(
                broken,
                "CEI 1*",
                CompetitionRuleset.FEI,
                ParticipationCategory.Senior,
                new RankingEntry(mine.Id, false),
                new RankingEntry(gone.Id, false)
            )
        );

        var rows = await RowsAsync(
            await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}")
        );

        Assert.Equal(3, rows.Count);
        var unplaced = rows.Single(x =>
            x.GetProperty("attributes").GetProperty("eventId").GetString() == broken.ToString()
        );
        Assert.False(unplaced.GetProperty("attributes").TryGetProperty("placing", out _));
        var unread = rows.Single(x =>
            x.GetProperty("attributes").GetProperty("eventId").GetString() == unreadable.ToString()
        );
        Assert.False(unread.GetProperty("attributes").TryGetProperty("placing", out _));
        Assert.Equal(
            "Fire",
            unread.GetProperty("attributes").GetProperty("other").GetProperty("displayName").GetString()
        );
        var placed = rows.Single(x =>
            x.GetProperty("attributes").GetProperty("eventId").GetString() == fine.ToString()
        );
        Assert.Equal(1, placed.GetProperty("attributes").GetProperty("placing").GetProperty("rank").GetInt32());
    }

    [Fact]
    public async Task A_Ranking_that_the_domain_does_not_accept_is_listed_without_a_placing_and_the_others_keep_theirs()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(
            _shared.MongoConnectionString,
            tenant,
            "Damaged ride",
            "Plovdiv",
            30
        );
        var mine = HistorySeed.Ride(past, 1, _ana, _fire);
        var own = Ranked(past, "CEI 1*", (mine, 2));
        var damaged = HistorySeed.Ranking(
            past,
            "Club Cup",
            CompetitionRuleset.Regional,
            ParticipationCategory.Senior,
            Entry(mine, 1)
        );
        await StoreAsync(tenant, mine, own, damaged);
        await HistorySeed.CountTwiceAsync(_shared.MongoConnectionString, damaged.Id, mine.Id);

        var rows = await RowsAsync(
            await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}")
        );

        var attributes = Assert.Single(rows).GetProperty("attributes");
        Assert.Equal(2, attributes.GetProperty("placing").GetProperty("rank").GetInt32()); // the own Ranking is told as it is
        var cup = Assert.Single(attributes.GetProperty("alsoCountedIn").EnumerateArray());
        Assert.Equal("Club Cup", cup.GetProperty("name").GetString());
        Assert.False(cup.TryGetProperty("placing", out _)); // and the one that is not a Ranking is listed without one
    }

    [Fact]
    public async Task A_ride_in_no_Ranking_of_its_own_has_no_placing_and_lists_the_Rankings_that_do_count_it()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Custom ride", "Plovdiv", 30);
        var mine = HistorySeed.Ride(past, 1, _ana, _fire);
        await StoreAsync(tenant, mine);
        await EventSeed.RankingAsync(
            _shared.MongoConnectionString,
            tenant,
            HistorySeed.Ranking(
                past,
                "Club Cup",
                CompetitionRuleset.Regional,
                ParticipationCategory.Senior,
                new RankingEntry(mine.Id, false, 1)
            )
        );

        var rows = await RowsAsync(
            await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}")
        );

        var attributes = Assert.Single(rows).GetProperty("attributes");
        Assert.False(attributes.TryGetProperty("placing", out _));
        Assert.Equal(
            "Club Cup",
            Assert.Single(attributes.GetProperty("alsoCountedIn").EnumerateArray()).GetProperty("name").GetString()
        );
    }

    [Fact]
    public async Task The_other_half_of_the_Combination_is_named_by_the_display_name_rule_of_the_ruleset_and_the_language_of_the_request()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Two rides", "Plovdiv", 30);
        var fei = HistorySeed.Ride(past, 1, _ana, _fire, "CEI 1*", CompetitionRuleset.FEI);
        var regional = HistorySeed.Ride(past, 2, _ana, _wind, "Regional 40", CompetitionRuleset.Regional);
        await StoreAsync(tenant, fei, regional);

        async Task<Dictionary<string, string>> NamesAsync(string? language)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/participations/history?filter=athleteId eq {_ana.Id}"
            );
            if (language != null)
            {
                request.Headers.AcceptLanguage.ParseAdd(language);
            }

            var rows = await RowsAsync(await me.Page.SendAsync(request));
            return rows.ToDictionary(
                x => x.GetProperty("attributes").GetProperty("competition").GetString()!,
                x => x.GetProperty("attributes").GetProperty("other").GetProperty("displayName").GetString()!
            );
        }

        var withoutLanguage = await NamesAsync(null);
        var inEnglish = await NamesAsync("en");
        var inBulgarian = await NamesAsync("bg");

        Assert.Equal("Fire", withoutLanguage["CEI 1*"]);
        Assert.Equal("Wind", withoutLanguage["Regional 40"]); // English when the request names no language
        Assert.Equal("Wind", inEnglish["Regional 40"]);
        Assert.Equal("Fire", inBulgarian["CEI 1*"]); // a FEI competition is told in English whatever the language
        Assert.Equal("Вятър", inBulgarian["Regional 40"]); // and a regional one in the language of the person
    }

    [Fact]
    public async Task A_list_is_cut_into_pages_and_sorted_by_the_members_of_a_row()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var places = new[] { "Burgas", "Ruse", "Varna", "Sofia", "Plovdiv" };
        var rides = new List<Participation>();
        for (var i = 0; i < places.Length; i++)
        {
            var past = await HistorySeed.HistoricAsync(
                _shared.MongoConnectionString,
                tenant,
                $"Ride {i}",
                places[i],
                (i + 1) * 10
            );
            var ride = HistorySeed.Ride(past, 1, _ana, _fire);
            rides.Add(ride);
            await StoreAsync(tenant, ride, Ranked(past, "CEI 1*", (ride, 1)));
        }

        async Task<(List<string> Locations, JsonElement Document)> PageAsync(string query)
        {
            var response = await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}{query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var document = await ApiSessions.ReadJsonAsync(response);
            return (
                [
                    .. document
                        .GetProperty("data")
                        .EnumerateArray()
                        .Select(x => x.GetProperty("attributes").GetProperty("location").GetString()!),
                ],
                document
            );
        }

        var first = await PageAsync("&page[size]=2");
        var second = await PageAsync("&page[size]=2&page[number]=2");
        var last = await PageAsync("&page[size]=2&page[number]=3");
        var byPlace = await PageAsync("&sort=location");
        var oldestFirst = await PageAsync("&sort=startDay");
        var everything = await PageAsync("&page[size]=5");

        Assert.Equal(["Burgas", "Ruse"], first.Locations); // the newest first: the Event ten days ago, then twenty
        Assert.Equal(2, first.Document.GetProperty("meta").GetProperty("page").GetProperty("size").GetInt32());
        Assert.Equal(1, first.Document.GetProperty("meta").GetProperty("page").GetProperty("number").GetInt32());
        Assert.Contains("page%5Bnumber%5D=2", first.Document.GetProperty("links").GetProperty("next").GetString());
        Assert.Equal(["Varna", "Sofia"], second.Locations);
        Assert.Equal(["Plovdiv"], last.Locations);
        Assert.False(last.Document.GetProperty("links").TryGetProperty("next", out _));
        Assert.Equal(["Burgas", "Plovdiv", "Ruse", "Sofia", "Varna"], byPlace.Locations);
        Assert.Equal(["Plovdiv", "Sofia", "Varna", "Ruse", "Burgas"], oldestFirst.Locations);
        Assert.Equal(["Burgas", "Ruse", "Varna", "Sofia", "Plovdiv"], everything.Locations);
        Assert.False(everything.Document.GetProperty("links").TryGetProperty("next", out _)); // nothing is left after a page that holds the rest
    }

    [Fact]
    public async Task Rides_of_Events_that_end_together_are_listed_the_later_started_first_and_then_by_the_id_of_the_ride()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var end = DateTimeOffset.UtcNow.Date.AddDays(-20).AddHours(23);
        var longer = await HistorySeed.EventAsync(
            _shared.MongoConnectionString,
            tenant,
            "Long",
            "Sofia",
            end.AddDays(-3),
            end
        );
        var shorter = await HistorySeed.EventAsync(
            _shared.MongoConnectionString,
            tenant,
            "Short",
            "Varna",
            end.AddDays(-1),
            end
        );
        var first = HistorySeed.Ride(shorter, 1, _ana, _fire, id: Guid.Parse("00000000-0000-0000-0000-00000000000a"));
        var second = HistorySeed.Ride(shorter, 2, _ana, _wind, id: Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff0"));
        var third = HistorySeed.Ride(longer, 1, _ana, _fire, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        await StoreAsync(tenant, second, third, first); // stored in an order of their own

        var rows = await RowsAsync(
            await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}")
        );

        Assert.Equal(
            new[] { first.Id, second.Id, third.Id }.Select(x => x.ToString()),
            rows.Select(x => x.GetProperty("id").GetString())
        );
    }

    [Theory]
    [InlineData("location")]
    [InlineData("-location")]
    public async Task Rides_that_are_equal_in_the_sort_of_the_caller_come_in_the_order_of_their_ids(string sort)
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "One Event", "Sofia", 30);
        var low = HistorySeed.Ride(past, 1, _ana, _fire, id: IdStartingWith("00"));
        var middle = HistorySeed.Ride(past, 2, _ana, _wind, id: IdStartingWith("80"));
        var high = HistorySeed.Ride(past, 3, _ana, _fire, id: IdStartingWith("ff"));
        await StoreAsync(tenant, high, low, middle); // stored in an order of their own

        var rows = await RowsAsync(
            await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}&sort={sort}")
        );

        Assert.Equal(
            new[] { low.Id, middle.Id, high.Id }.Select(x => x.ToString()),
            rows.Select(x => x.GetProperty("id").GetString())
        ); // one location, so the sort does not tell them apart, and a page of them must not depend on how they are stored
    }

    [Fact]
    public async Task The_person_may_be_in_parentheses_and_the_filter_may_have_spaces_around_it()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Ride", "Plovdiv", 30);
        var ride = HistorySeed.Ride(past, 1, _ana, _fire);
        await StoreAsync(tenant, ride);

        var inParentheses = await me.Page.GetAsync(
            $"/api/participations/history?filter=(athleteId eq {_ana.Id}) and category eq 'Senior'"
        );
        var spaced = await me.Page.GetAsync(
            $"/api/participations/history?filter={Uri.EscapeDataString($"  athleteId eq {_ana.Id}  ")}"
        );
        var ofTheHorse = await me.Page.GetAsync($"/api/participations/history?filter=horseId eq {_fire.Id}");

        foreach (var response in new[] { inParentheses, spaced, ofTheHorse })
        {
            Assert.Equal(ride.Id.ToString(), Assert.Single(await RowsAsync(response)).GetProperty("id").GetString());
        }
    }

    [Theory]
    [InlineData("", "subject-required")] // no filter: whose?
    [InlineData("&sort=location", "subject-required")]
    [InlineData("?filter=category eq 'Senior'", "subject-required")] // not the Athlete or the Horse
    [InlineData("?filter=athleteId eq nobody", "subject-required")] // not a Guid
    [InlineData("?filter=eventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301", "subject-required")] // the Event is no Participant
    [InlineData("?filter=horseId eq 5 and athleteId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301", "subject-required")] // the first one has to be a person
    [InlineData(
        "?filter=athleteId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301 or horseId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301",
        "subject-required"
    )] // or is not and
    public async Task A_list_that_names_nobody_is_refused_with_a_code(string query, string code)
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var path = query.StartsWith('?')
            ? "/api/participations/history" + query
            : "/api/participations/history" + (query.Length == 0 ? "" : "?" + query.TrimStart('&'));

        var response = await me.Page.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_filter_that_goes_on_with_or_cannot_widen_the_list_to_the_rides_of_somebody_else()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Ride", "Plovdiv", 30);
        var mine = HistorySeed.Ride(past, 1, _ana, _fire);
        var yours = HistorySeed.Ride(past, 2, _boris, _wind);
        await StoreAsync(tenant, mine, yours);

        var response = await me.Page.GetAsync(
            $"/api/participations/history?filter=athleteId eq {_ana.Id} and horseId eq {Guid.NewGuid()} or category eq 'Senior'"
        );

        var rows = await RowsAsync(response);
        Assert.Equal(mine.Id.ToString(), Assert.Single(rows).GetProperty("id").GetString()); // the rides are read for the Athlete named first
    }

    [Theory]
    [InlineData("&sort=nonsense", "invalid-sort")]
    [InlineData("&sort=-", "invalid-sort")]
    [InlineData("&page[size]=0", "invalid-page")]
    [InlineData("&page[size]=501", "invalid-page")]
    [InlineData("&page[number]=0", "invalid-page")]
    [InlineData("&include=horse", "unsupported-parameter")]
    [InlineData("&fields[participation-histories]=location", "unsupported-parameter")]
    [InlineData(" and nonsense eq 1", "invalid-filter")]
    [InlineData(" and eventIsDeleted eq false", "invalid-filter")] // what the database needs to leave a ride out is not a member of it
    [InlineData(" and substring(competition,100) eq 'x'", "invalid-filter")] // valid, and the database cannot run it on a text that short
    [InlineData(" and 1 div 0 eq 1", "invalid-filter")]
    [InlineData(" and other/displayName eq 'Fire'", "invalid-filter")] // what a row keeps to itself cannot be asked for
    [InlineData(" and placing/rank eq 1", "invalid-filter")]
    [InlineData(" and other eq null", "invalid-filter")]
    [InlineData(" and placing eq null", "invalid-filter")]
    [InlineData(" and alsoCountedIn/$count gt 0", "invalid-filter")]
    [InlineData("&sort=other/displayName", "invalid-sort")]
    public async Task What_the_list_does_not_take_is_refused_with_the_code_the_skill_names(string more, string code)
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Ride", "Plovdiv", 30);
        var ride = HistorySeed.Ride(past, 1, _ana, _fire);
        await StoreAsync(tenant, ride, Ranked(past, "CEI 1*", (ride, 1)));

        var response = await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}{more}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_list_filtered_further_is_cut_to_what_the_filter_keeps()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Ride", "Plovdiv", 30);
        var onFire = HistorySeed.Ride(past, 1, _ana, _fire);
        var onWind = HistorySeed.Ride(past, 2, _ana, _wind, "CEI 2*");
        await StoreAsync(tenant, onFire, onWind);

        var onTheHorse = await RowsAsync(
            await me.Page.GetAsync(
                $"/api/participations/history?filter=athleteId eq {_ana.Id} and horseId eq {_wind.Id}"
            )
        );
        var byCompetition = await RowsAsync(
            await me.Page.GetAsync(
                $"/api/participations/history?filter=athleteId eq {_ana.Id} and competition eq 'CEI 1*'"
            )
        );

        var inTheText = await RowsAsync(
            await me.Page.GetAsync(
                $"/api/participations/history?filter=athleteId eq {_ana.Id} and contains(competition,'2*')"
            )
        );
        var beforeTheYear2000 = await RowsAsync(
            await me.Page.GetAsync(
                $"/api/participations/history?filter=athleteId eq {_ana.Id} and startDay lt 2000-01-01T00:00:00Z"
            )
        );
        var afterTheYear2000 = await RowsAsync(
            await me.Page.GetAsync(
                $"/api/participations/history?filter=athleteId eq {_ana.Id} and startDay gt 2000-01-01T00:00:00Z"
            )
        );

        Assert.Equal(onWind.Id.ToString(), Assert.Single(onTheHorse).GetProperty("id").GetString());
        Assert.Equal(onFire.Id.ToString(), Assert.Single(byCompetition).GetProperty("id").GetString());
        Assert.Equal(onWind.Id.ToString(), Assert.Single(inTheText).GetProperty("id").GetString());
        Assert.Empty(beforeTheYear2000);
        Assert.Equal(2, afterTheYear2000.Count);
    }

    [Fact]
    public async Task A_list_is_filtered_and_sorted_by_the_name_of_the_Event_and_by_the_ruleset_of_the_ride()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var spring = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Spring ride", "Sofia", 10);
        var autumn = await HistorySeed.HistoricAsync(_shared.MongoConnectionString, tenant, "Autumn ride", "Varna", 30);
        var onFire = HistorySeed.Ride(spring, 1, _ana, _fire);
        var onWind = HistorySeed.Ride(autumn, 1, _ana, _wind, "Regional 40", CompetitionRuleset.Regional);
        await StoreAsync(tenant, onFire, onWind);

        async Task<List<string?>> IdsAsync(string more)
        {
            var response = await me.Page.GetAsync($"/api/participations/history?filter=athleteId eq {_ana.Id}{more}");
            return [.. (await RowsAsync(response)).Select(x => x.GetProperty("id").GetString())];
        }

        Assert.Equal([onWind.Id.ToString()], await IdsAsync(" and eventName eq 'Autumn ride'"));
        Assert.Equal([onWind.Id.ToString()], await IdsAsync(" and ruleset eq 'Regional'"));
        Assert.Equal([onFire.Id.ToString()], await IdsAsync(" and ruleset eq 'FEI'"));
        Assert.Equal([onWind.Id.ToString(), onFire.Id.ToString()], await IdsAsync("&sort=eventName")); // Autumn, Spring
        Assert.Equal([onWind.Id.ToString(), onFire.Id.ToString()], await IdsAsync("&sort=-ruleset")); // Regional, FEI
    }

    [Fact]
    public async Task The_public_Participations_resource_refuses_an_anonymous_filter_on_the_Athlete_or_the_Horse_and_is_otherwise_unchanged()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var me = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var live = await HistorySeed.LiveAsync(_shared.MongoConnectionString, tenant, "Live ride", "Plovdiv");
        var ride = HistorySeed.Ride(live, 1, _ana, _fire);
        await StoreAsync(tenant, ride);
        var inTheEvent = $"/api/participations?filter=eventId eq {live}";

        using var plain = await client.GetAsync(inTheEvent);
        using var byAthlete = await client.GetAsync($"{inTheEvent} and combination/athlete/id eq {_ana.Id}");
        using var byHorse = await client.GetAsync($"{inTheEvent} and combination/horse/id eq {_fire.Id}");
        using var byHorseName = await client.GetAsync($"{inTheEvent} and combination/horse/name eq 'Огън'");
        using var withoutEvent = await client.GetAsync(
            $"/api/participations?filter=combination/athlete/id eq {_ana.Id}"
        );
        using var byNumber = await client.GetAsync($"{inTheEvent} and combination/number eq 1");
        using var ofAnother = await client.GetAsync($"/api/participations?filter=horseId eq {live}");
        using var byCompetition = await client.GetAsync($"{inTheEvent} and competition/name eq 'Horse Show'");
        using var byATextAboutAnAthlete = await client.GetAsync(
            $"{inTheEvent} and competition/name eq 'combination/athlete/id'"
        );
        using var detail = await client.GetAsync($"/api/participations/{ride.Id}");
        var signedInByAthlete = await me.Page.GetAsync($"{inTheEvent} and combination/athlete/id eq {_ana.Id}");

        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, byAthlete.StatusCode);
        Assert.Equal("not-signed-in", await ErrorCodeAsync(byAthlete));
        Assert.Equal(HttpStatusCode.Unauthorized, byHorse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, byHorseName.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutEvent.StatusCode); // 401 before it is told that it names no Event
        Assert.Equal(HttpStatusCode.OK, byNumber.StatusCode); // another member of the Combination
        Assert.Equal("event-required", await ErrorCodeAsync(ofAnother)); // the list starts with the Event, not with any member
        Assert.Equal(HttpStatusCode.OK, byCompetition.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byATextAboutAnAthlete.StatusCode); // a word of a text is no member
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode); // the detail stays public
        Assert.Equal(HttpStatusCode.OK, signedInByAthlete.StatusCode);
        var rows = (await ApiSessions.ReadJsonAsync(signedInByAthlete)).GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(ride.Id.ToString(), Assert.Single(rows).GetProperty("id").GetString());
    }

    [Fact]
    public async Task The_Participations_collection_is_indexed_on_the_Athlete_and_the_Horse_when_the_host_starts_and_again_without_harm()
    {
        await using var first = new ApiFactory(_shared.MongoConnectionString);
        using var _ = ApiClients.Of(first); // starts the host
        await using var second = new ApiFactory(_shared.MongoConnectionString);
        using var __ = ApiClients.Of(second);

        var indexes = await (
            await new MongoClient(_shared.MongoConnectionString)
                .GetDatabase(MongoLease.DatabaseNameOf(_shared.MongoConnectionString))
                .GetCollection<BsonDocument>("event_participations")
                .Indexes.ListAsync()
        ).ToListAsync();

        var keysByName = indexes.ToDictionary(x => x["name"].AsString, x => x["key"].AsBsonDocument);
        Assert.Equal(new BsonDocument("Combination.Athlete._id", 1), keysByName["participations_of_an_athlete"]);
        Assert.Equal(new BsonDocument("Combination.Horse._id", 1), keysByName["participations_of_a_horse"]);
    }

    /// <summary>Stores the Participations and the Rankings given in the Tenant, as the Api keeps them.</summary>
    async Task StoreAsync(string tenant, params object[] documents)
    {
        foreach (var document in documents)
        {
            if (document is Participation ride)
            {
                await EventSeed.ParticipationAsync(_shared.MongoConnectionString, tenant, ride);
            }
            else
            {
                await EventSeed.RankingAsync(_shared.MongoConnectionString, tenant, (Ranking)document);
            }
        }
    }

    /// <summary>A new id that is ordered by the two hex digits it starts with, before or after another that starts with others.</summary>
    static Guid IdStartingWith(string digits)
    {
        return Guid.Parse(digits + Guid.NewGuid().ToString("N")[2..]);
    }

    static RankingEntry Entry(Participation ride, int rank)
    {
        return new RankingEntry(ride.Id, false, rank);
    }

    /// <summary>The Ranking of the Competition and the Category that rides are of by default, with the ranks stored.</summary>
    static Ranking Ranked(Guid eventId, string competition, params (Participation Ride, int Rank)[] entries)
    {
        return HistorySeed.Ranking(
            eventId,
            competition,
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            [.. entries.Select(x => new RankingEntry(x.Ride.Id, false, x.Rank))]
        );
    }

    static async Task<List<JsonElement>> RowsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").EnumerateArray()];
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
