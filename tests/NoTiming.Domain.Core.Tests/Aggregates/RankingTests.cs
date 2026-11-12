using NC.Domain.Exceptions;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Core.Objects;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Core.Tests.Aggregates;

public sealed class RankingTests
{
    static readonly DateTimeOffset START = new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WhenSomeEntriesHaveStoredRankAndOthersDoNot_ShouldThrow()
    {
        var ranked = CreateParticipation(1);
        var unranked = CreateParticipation(2);

        Assert.Throws<DomainException>(
            () => CreateRanking([new RankingEntry(ranked, 1, false), new RankingEntry(unranked, null, false)])
        );
    }

    [Fact]
    public void Constructor_WhenEveryEntryHasStoredRank_ShouldKeepEntries()
    {
        var first = CreateParticipation(1);
        var second = CreateParticipation(2);

        var ranking = CreateRanking([new RankingEntry(first, 1, false), new RankingEntry(second, 2, false)]);

        Assert.Equal([1, 2], ranking.Entries.Select(x => x.Rank));
    }

    [Fact]
    public void Constructor_WhenNoEntryHasStoredRank_ShouldKeepEntries()
    {
        var first = CreateParticipation(1);
        var second = CreateParticipation(2);

        var ranking = CreateRanking([new RankingEntry(first, null, false), new RankingEntry(second, null, false)]);

        Assert.Equal([null, null], ranking.Entries.Select(x => x.Rank));
    }

    static Ranking CreateRanking(IEnumerable<RankingEntry> entries)
    {
        return new Ranking(
            "Competition",
            true,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            eventId: 10,
            id: 99
        );
    }

    static Participation CreateParticipation(int number)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, number);
        var horse = new Horse($"Horse {number}", null, null, number);
        var combination = new Combination(number, athlete, horse, null, "20", null, number);

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", true),
            combination,
            new PhaseCollection([CreatePhase(START)]),
            null,
            eventId: 10,
            id: number
        );
    }

    static Phase CreatePhase(DateTimeOffset start)
    {
        return new Phase(
            "GATE1",
            20,
            40,
            null,
            true,
            null,
            Timestamp.Create(start),
            null,
            null,
            null,
            false,
            false,
            false
        );
    }
}
