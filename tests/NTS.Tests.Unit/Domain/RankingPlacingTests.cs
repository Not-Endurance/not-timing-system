using Not.Exceptions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Tests.Unit.Application;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// What a ride placed as, in one Ranking that counts it (#638, ADR-0006): the stored rank of its entry once the Ranking is final,
/// the rank the Results give it while it is not, the code of the elimination as the Results show it, or not ranked. A list of the
/// Participations of an Athlete or a Horse asks it for each ride without composing a whole Result for the Ranking when the placings
/// are stored.
/// </summary>
public sealed class RankingPlacingTests
{
    static readonly Guid EVENT_ID = TestId.Of(1);

    [Fact]
    public void A_final_Ranking_gives_the_stored_rank_of_the_entry_without_reading_the_other_Participations()
    {
        var first = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-4));
        var second = ParticipationFixtures.CompletedAt(2, DateTimeOffset.Now.AddHours(-3));
        var ranking = RankingOf(new RankingEntry(first.Id, false, 2), new RankingEntry(second.Id, false, 1));

        var placing = ranking.PlacingOf(first, [first], null); // the other ride is not given: a stored rank needs none

        Assert.Equal(Placing.Ranked(2), placing);
        Assert.Equal(PlacingKind.Ranked, placing.Kind);
        Assert.Equal(2, placing.Rank);
        Assert.Null(placing.Code);
    }

    [Fact]
    public void A_stored_rank_is_what_is_shown_even_when_the_times_would_now_rank_the_ride_otherwise()
    {
        var quick = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-4));
        var slow = ParticipationFixtures.CompletedAt(2, DateTimeOffset.Now.AddHours(-2));
        var ranking = RankingOf(new RankingEntry(quick.Id, false, 2), new RankingEntry(slow.Id, false, 1));

        Assert.Equal(Placing.Ranked(2), ranking.PlacingOf(quick, [quick, slow], null));
        Assert.Equal(Placing.Ranked(1), ranking.PlacingOf(slow, [quick, slow], null));
    }

    [Fact]
    public void A_Ranking_that_is_not_final_ranks_the_ride_in_memory_as_the_Results_do()
    {
        var arrive = DateTimeOffset.Now.AddHours(-4);
        var early = ParticipationFixtures.CompletedAt(1, arrive);
        var late = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(20));
        var ranking = RankingOf(new RankingEntry(late.Id, false), new RankingEntry(early.Id, false));

        Assert.False(ranking.IsFinal);
        Assert.Equal(Placing.Ranked(1), ranking.PlacingOf(early, [early, late], null));
        Assert.Equal(Placing.Ranked(2), ranking.PlacingOf(late, [early, late], null));
        Assert.Equal(
            new Result(ranking, [early, late]).Entries.Select(x => x.Rank),
            [ranking.PlacingOf(early, [early, late], null).Rank, ranking.PlacingOf(late, [early, late], null).Rank]
        ); // the Results order the entries by their rank: early first
    }

    [Fact]
    public void The_only_entry_of_a_Ranking_that_is_not_final_is_rank_1_though_the_Results_do_not_rank_one_entry()
    {
        var only = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-3));
        var ranking = RankingOf(new RankingEntry(only.Id, false));

        Assert.Null(Assert.Single(new Result(ranking, [only]).Entries).Rank);
        Assert.Equal(Placing.Ranked(1), ranking.PlacingOf(only, [only], null));
    }

    [Fact]
    public void An_eliminated_ride_shows_the_code_the_Results_show_and_not_a_number_whatever_rank_it_holds()
    {
        var eliminated = ParticipationFixtures.Eliminated(3);
        var other = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-4));
        var ranking = RankingOf(new RankingEntry(other.Id, false, 1), new RankingEntry(eliminated.Id, false, 2));

        var placing = ranking.PlacingOf(eliminated, [eliminated], null);

        Assert.Equal(PlacingKind.Eliminated, placing.Kind);
        Assert.Equal(eliminated.Eliminated!.ToString(), placing.Code);
        Assert.Null(placing.Rank);
        Assert.Equal(placing, ranking.PlacingOf(eliminated, [eliminated, other], null)); // in memory too
    }

    [Fact]
    public void An_elimination_shows_its_codes_as_the_Results_show_them_and_not_the_bare_code_of_its_kind()
    {
        var failed = ParticipationFixtures.Arrived(
            4,
            new FailedToQualify([FailToQualifyCode.GA, FailToQualifyCode.ME])
        );
        var ranking = RankingOf(new RankingEntry(failed.Id, false, 1));

        var placing = ranking.PlacingOf(failed, [failed], null);

        Assert.Equal(Placing.Eliminated("FTQ GA+ME"), placing);
        Assert.NotEqual(failed.Eliminated!.Code, placing.Code);
    }

    [Fact]
    public void A_ride_that_is_not_ranked_shows_so_and_not_the_number_it_is_listed_under()
    {
        var counted = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-4));
        var notRanked = ParticipationFixtures.CompletedAt(2, DateTimeOffset.Now.AddHours(-3));
        var ranking = RankingOf(new RankingEntry(counted.Id, false, 1), new RankingEntry(notRanked.Id, true, 2));

        var placing = ranking.PlacingOf(notRanked, [notRanked], null);

        Assert.Equal(Placing.NotRanked(), placing);
        Assert.Equal(PlacingKind.NotRanked, placing.Kind);
        Assert.Null(placing.Rank);
        Assert.Null(placing.Code);
    }

    [Fact]
    public void A_ride_that_is_eliminated_and_not_ranked_shows_the_elimination()
    {
        var eliminated = ParticipationFixtures.Eliminated(3);
        var ranking = RankingOf(new RankingEntry(eliminated.Id, true, 1));

        Assert.Equal(PlacingKind.Eliminated, ranking.PlacingOf(eliminated, [eliminated], null).Kind);
    }

    [Fact]
    public void A_ride_gets_the_placing_of_its_own_in_each_Ranking_that_counts_it()
    {
        var arrive = DateTimeOffset.Now.AddHours(-4);
        var shared = ParticipationFixtures.CompletedAt(1, arrive);
        var other = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(10));
        var regular = RankingOf(new RankingEntry(shared.Id, false, 1), new RankingEntry(other.Id, false, 2));
        var custom = RankingWithId(
            TestId.Of(98),
            new RankingEntry(shared.Id, true, 2),
            new RankingEntry(other.Id, false, 1)
        );

        Assert.Equal(Placing.Ranked(1), regular.PlacingOf(shared, [shared], null));
        Assert.Equal(Placing.NotRanked(), custom.PlacingOf(shared, [shared], null));
    }

    [Fact]
    public void A_Ranking_that_does_not_count_the_ride_has_no_placing_for_it()
    {
        var counted = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-4));
        var stranger = ParticipationFixtures.CompletedAt(2, DateTimeOffset.Now.AddHours(-3));
        var ranking = RankingOf(new RankingEntry(counted.Id, false, 1));

        Assert.Throws<GuardException>(() => ranking.PlacingOf(stranger, [stranger], null));
    }

    [Fact]
    public void A_Ranking_that_counts_a_Participation_that_is_not_there_cannot_be_ranked_in_memory()
    {
        var present = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-4));
        var missing = ParticipationFixtures.CompletedAt(2, DateTimeOffset.Now.AddHours(-3));
        var ranking = RankingOf(new RankingEntry(present.Id, false), new RankingEntry(missing.Id, false));

        Assert.Throws<GuardException>(() => ranking.PlacingOf(present, [present], null));
    }

    [Fact]
    public void A_final_Ranking_is_read_for_its_stored_rank_even_when_it_counts_a_Participation_that_is_not_given()
    {
        var present = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-4));
        var missing = ParticipationFixtures.CompletedAt(2, DateTimeOffset.Now.AddHours(-3));
        var ranking = RankingOf(new RankingEntry(present.Id, false, 1), new RankingEntry(missing.Id, false, 2));

        Assert.Equal(Placing.Ranked(1), ranking.PlacingOf(present, [present], null));
    }

    [Fact]
    public void A_Ranking_with_only_some_ranks_is_not_final_and_is_ranked_in_memory_whatever_ranks_it_holds()
    {
        var arrive = DateTimeOffset.Now.AddHours(-4);
        var early = ParticipationFixtures.CompletedAt(1, arrive);
        var late = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(30));
        var ranking = RankingOf(new RankingEntry(late.Id, false, 1), new RankingEntry(early.Id, false)); // a rank that the times deny

        Assert.False(ranking.IsFinal);
        Assert.Equal(Placing.Ranked(1), ranking.PlacingOf(early, [early, late], null));
        Assert.Equal(Placing.Ranked(2), ranking.PlacingOf(late, [early, late], null));
    }

    [Theory]
    [InlineData("Competition", CompetitionRuleset.Regional, ParticipationCategory.Senior, true)]
    [InlineData("Another Competition", CompetitionRuleset.Regional, ParticipationCategory.Senior, false)]
    [InlineData("Competition", CompetitionRuleset.FEI, ParticipationCategory.Senior, false)]
    [InlineData("Competition", CompetitionRuleset.Regional, ParticipationCategory.Children, false)]
    public void A_Ranking_is_the_own_Ranking_of_a_ride_when_it_carries_the_name_and_ruleset_of_its_Competition_and_its_Category(
        string name,
        CompetitionRuleset ruleset,
        ParticipationCategory category,
        bool isOwn
    )
    {
        var ride = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-4)); // "Competition", Regional, Senior
        var ranking = new Ranking(
            name,
            ruleset,
            category,
            null,
            null,
            null,
            null,
            null,
            [new RankingEntry(ride.Id, false, 1)],
            EVENT_ID,
            TestId.Of(97)
        );

        Assert.Equal(isOwn, ranking.IsOwnOf(ride));
    }

    static Ranking RankingOf(params RankingEntry[] entries)
    {
        return RankingWithId(TestId.Of(99), entries);
    }

    static Ranking RankingWithId(Guid id, params RankingEntry[] entries)
    {
        return new Ranking(
            "CEI 1*",
            CompetitionRuleset.Regional,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            EVENT_ID,
            id
        );
    }
}
