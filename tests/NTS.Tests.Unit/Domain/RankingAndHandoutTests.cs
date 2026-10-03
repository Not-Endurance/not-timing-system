using Not.Domain.Exceptions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// A Ranking and a Handout hold the id of a Participation and nothing else of it (ADR-0006). One that holds none, as
/// a document stored before this shape reads, is refused rather than loaded as something it is not.
/// </summary>
public sealed class RankingAndHandoutTests
{
    [Fact]
    public void A_Ranking_cannot_list_a_Participation_twice()
    {
        var once = new RankingEntry(TestId.Of(1), false);
        var again = new RankingEntry(TestId.Of(1), true);

        Assert.Throws<DomainPropertyException>(() => CreateRanking([once, again]));
    }

    [Fact]
    public void A_Ranking_lists_the_same_Participation_once_but_any_number_of_others()
    {
        var ranking = CreateRanking(
            [
                new RankingEntry(TestId.Of(1), false),
                new RankingEntry(TestId.Of(2), true, 3),
                new RankingEntry(TestId.Of(3), false),
            ]
        );

        Assert.Equal([TestId.Of(1), TestId.Of(2), TestId.Of(3)], ranking.Entries.Select(x => x.ParticipationId));
        Assert.Equal([null, 3, null], ranking.Entries.Select(x => x.Rank));
    }

    [Fact]
    public void A_Ranking_entry_without_a_Participation_is_refused()
    {
        Assert.Throws<DomainPropertyException>(() => new RankingEntry(Guid.Empty, false));
    }

    [Fact]
    public void A_Handout_without_a_Participation_is_refused()
    {
        Assert.Throws<DomainPropertyException>(() => new Handout(TestId.Of(10), Guid.Empty));
    }

    static Ranking CreateRanking(IEnumerable<RankingEntry> entries)
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
            eventId: TestId.Of(10)
        );
    }
}
