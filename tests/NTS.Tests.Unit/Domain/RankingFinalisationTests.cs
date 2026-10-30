using Not.Exceptions;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Tests.Unit.Application;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// A Ranking keeps its final placings once its Event is Historic (ADR-0006, #640): each entry's rank is stored, composed by
/// the code that composes the Results, so that nothing that lists Participations has to compose a whole Result to learn one
/// placing and an Event keeps the placings it was printed with. While the Event is Live nothing is stored and the Results rank
/// in memory. The Results of a final Ranking read the stored ranks.
/// </summary>
public sealed class RankingFinalisationTests
{
    static readonly Guid EVENT_ID = TestId.Of(1);

    [Fact]
    public void Finalising_stores_the_ranks_the_Results_compute_the_eliminated_and_the_not_ranked_numbered_as_they_are()
    {
        var arrive = DateTimeOffset.Now.AddHours(-4);
        var early = ParticipationFixtures.CompletedAt(1, arrive);
        var late = ParticipationFixtures.CompletedAt(5, arrive.AddMinutes(20));
        var incomplete = ParticipationFixtures.Active(4);
        var notRanked = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(5));
        var eliminated = ParticipationFixtures.Eliminated(3);
        Participation[] participations = [early, late, incomplete, notRanked, eliminated];
        var ranking = RankingOf(
            new RankingEntry(eliminated.Id, false),
            new RankingEntry(notRanked.Id, true),
            new RankingEntry(late.Id, false),
            new RankingEntry(incomplete.Id, false),
            new RankingEntry(early.Id, false)
        );
        var computed = new Result(ranking, participations).Entries.ToDictionary(x => x.ParticipationId, x => x.Rank);

        var finalisation = ranking.Finalise(participations, null, EventStage.Historic);

        Assert.Equal(RankingFinalisationOutcome.Finalised, finalisation.Outcome);
        Assert.True(finalisation.Ranking.IsFinal);
        Assert.Equal(computed, finalisation.Ranking.Entries.ToDictionary(x => x.ParticipationId, x => x.Rank));
        Assert.Equal(
            [early.Id, late.Id, incomplete.Id, notRanked.Id, eliminated.Id],
            finalisation.Ranking.Entries.OrderBy(x => x.Rank).Select(x => x.ParticipationId)
        );
    }

    [Fact]
    public void Finalising_changes_nothing_but_the_ranks_and_keeps_the_order_of_the_entries()
    {
        var arrive = DateTimeOffset.Now.AddHours(-4);
        var first = ParticipationFixtures.CompletedAt(1, arrive);
        var second = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(10));
        var ranking = new Ranking(
            "Junior 120",
            CompetitionRuleset.FEI,
            ParticipationCategory.JuniorOrYoungAdult,
            "2025_CI_0001",
            "CEI1*",
            "COMPETITION-1",
            "FEI rule",
            "SCHEDULE-2",
            [new RankingEntry(second.Id, true), new RankingEntry(first.Id, false)],
            EVENT_ID,
            TestId.Of(97)
        );

        var finalised = ranking.Finalise([first, second], null, EventStage.Historic).Ranking;

        Assert.Equal(ranking.Id, finalised.Id);
        Assert.Equal(ranking.EventId, finalised.EventId);
        Assert.Equal(ranking.Name, finalised.Name);
        Assert.Equal(ranking.Ruleset, finalised.Ruleset);
        Assert.Equal(ranking.Category, finalised.Category);
        Assert.Equal(ranking.FeiEventId, finalised.FeiEventId);
        Assert.Equal(ranking.FeiEventCode, finalised.FeiEventCode);
        Assert.Equal(ranking.FeiCompetitionId, finalised.FeiCompetitionId);
        Assert.Equal(ranking.FeiRule, finalised.FeiRule);
        Assert.Equal(ranking.FeiScheduleNumber, finalised.FeiScheduleNumber);
        Assert.Equal([second.Id, first.Id], finalised.Entries.Select(x => x.ParticipationId));
        Assert.Equal([true, false], finalised.Entries.Select(x => x.IsNotRanked));
        Assert.Equal([2, 1], finalised.Entries.Select(x => x.Rank));
    }

    [Fact]
    public void A_ride_counted_in_two_Rankings_gets_the_rank_of_its_own_in_each()
    {
        var arrive = DateTimeOffset.Now.AddHours(-4);
        var shared = ParticipationFixtures.CompletedAt(1, arrive);
        var other = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(10));
        Participation[] participations = [shared, other];
        var regular = RankingOf(new RankingEntry(shared.Id, false), new RankingEntry(other.Id, false));
        var custom = RankingWithId(TestId.Of(98), new RankingEntry(shared.Id, true), new RankingEntry(other.Id, false));

        var inTheRegularOne = regular.Finalise(participations, null, EventStage.Historic).Ranking;
        var inTheCustomOne = custom.Finalise(participations, null, EventStage.Historic).Ranking;

        Assert.Equal(1, inTheRegularOne.Entries.Single(x => x.ParticipationId == shared.Id).Rank);
        Assert.Equal(2, inTheCustomOne.Entries.Single(x => x.ParticipationId == shared.Id).Rank);
        Assert.Equal(2, inTheRegularOne.Entries.Single(x => x.ParticipationId == other.Id).Rank);
        Assert.Equal(1, inTheCustomOne.Entries.Single(x => x.ParticipationId == other.Id).Rank);
    }

    [Fact]
    public void A_Ranking_with_a_single_entry_is_finalised_like_any_other_and_its_only_entry_is_rank_1()
    {
        var only = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-3));
        var ranking = RankingOf(new RankingEntry(only.Id, false));

        var finalisation = ranking.Finalise([only], null, EventStage.Historic);

        Assert.Equal(RankingFinalisationOutcome.Finalised, finalisation.Outcome);
        Assert.Equal(1, Assert.Single(finalisation.Ranking.Entries).Rank);
        var results = new Result(finalisation.Ranking, [only]);
        Assert.False(results.IsRanked); // a Ranking of one is still not a ranking
        Assert.Equal(1, Assert.Single(results.Entries).Rank);
    }

    [Fact]
    public void A_Ranking_with_only_some_ranks_stored_is_reported_and_left_alone()
    {
        var arrive = DateTimeOffset.Now.AddHours(-4);
        var first = ParticipationFixtures.CompletedAt(1, arrive);
        var second = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(10));
        var ranking = RankingOf(new RankingEntry(first.Id, false, 1), new RankingEntry(second.Id, false));

        var finalisation = ranking.Finalise([first, second], null, EventStage.Historic);

        Assert.Equal(RankingFinalisationOutcome.SomePlacingsStored, finalisation.Outcome);
        Assert.Same(ranking, finalisation.Ranking);
        Assert.False(ranking.IsFinal);
    }

    [Fact]
    public void Finalising_twice_changes_nothing()
    {
        var arrive = DateTimeOffset.Now.AddHours(-4);
        var first = ParticipationFixtures.CompletedAt(1, arrive);
        var second = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(10));
        Participation[] participations = [first, second];
        var once = RankingOf(new RankingEntry(first.Id, false), new RankingEntry(second.Id, false))
            .Finalise(participations, null, EventStage.Historic)
            .Ranking;

        var again = once.Finalise(participations, null, EventStage.Historic);

        Assert.Equal(RankingFinalisationOutcome.AlreadyFinal, again.Outcome);
        Assert.Same(once, again.Ranking);
    }

    [Fact]
    public void A_Ranking_with_no_entries_has_nothing_to_finalise()
    {
        var ranking = RankingOf();

        var finalisation = ranking.Finalise([], null, EventStage.Historic);

        Assert.True(ranking.IsFinal);
        Assert.Equal(RankingFinalisationOutcome.AlreadyFinal, finalisation.Outcome);
    }

    [Theory]
    [InlineData(EventStage.Live)]
    [InlineData(EventStage.Unstarted)]
    public void A_Ranking_is_not_finalised_while_its_Event_is_not_Historic(EventStage stage)
    {
        var participation = ParticipationFixtures.CompletedAt(1, DateTimeOffset.Now.AddHours(-3));
        var ranking = RankingOf(new RankingEntry(participation.Id, false));

        var refused = Assert.Throws<GuardException>(() => ranking.Finalise([participation], null, stage));

        Assert.Contains(ranking.Id.ToString(), refused.Message);
        Assert.False(ranking.IsFinal);
    }

    [Fact]
    public void The_Results_of_a_final_Ranking_equal_its_stored_ranks_even_where_the_ranker_would_now_order_differently()
    {
        var arrive = DateTimeOffset.Now.AddHours(-4);
        var earlier = ParticipationFixtures.CompletedAt(1, arrive);
        var later = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(30));
        // The ranker puts the one that arrived first first; the placings stored when the Event ended say otherwise.
        var final = RankingOf(new RankingEntry(earlier.Id, false, 2), new RankingEntry(later.Id, false, 1));

        var results = new Result(final, [earlier, later]);

        Assert.Equal([later.Id, earlier.Id], results.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], results.Entries.Select(x => x.Rank));
        Assert.True(results.IsRanked);
    }

    [Fact]
    public void The_Results_of_a_Ranking_that_is_not_final_are_ranked_in_memory_as_before()
    {
        var arrive = DateTimeOffset.Now.AddHours(-4);
        var earlier = ParticipationFixtures.CompletedAt(1, arrive);
        var later = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(30));
        var someStored = RankingOf(new RankingEntry(earlier.Id, false, 2), new RankingEntry(later.Id, false));

        var results = new Result(someStored, [earlier, later]);

        Assert.Equal([earlier.Id, later.Id], results.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], results.Entries.Select(x => x.Rank));
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
