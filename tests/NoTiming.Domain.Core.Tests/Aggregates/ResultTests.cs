using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Core.Objects;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Core.Tests.Aggregates;

public sealed class ResultTests
{
    static readonly DateTimeOffset START = new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WhenEntriesHaveNoArriveTime_ShouldRankThemLast()
    {
        var notArrived = CreateParticipation(1, arrive: null, present: null);
        var arrivedEarlier = CreateParticipation(2, START.AddHours(1), present: null);
        var ranking = CreateRanking([new RankingEntry(notArrived, null, false), new RankingEntry(arrivedEarlier, null, false)]);

        var result = new Result(ranking);

        Assert.Equal([arrivedEarlier.Id, notArrived.Id], result.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], result.Entries.Select(x => x.Rank));
    }

    [Fact]
    public void Constructor_WhenEveryEntryHasStoredRank_ShouldKeepStoredRanks()
    {
        var earlier = CreateParticipation(1, START.AddHours(1), present: START.AddHours(1).AddMinutes(5));
        var later = CreateParticipation(2, START.AddHours(2), present: START.AddHours(2).AddMinutes(5));
        var ranking = CreateRanking([new RankingEntry(earlier, 2, false), new RankingEntry(later, 1, false)]);

        var result = new Result(ranking);

        Assert.Equal([later.Id, earlier.Id], result.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], result.Entries.Select(x => x.Rank));
    }

    [Fact]
    public void Constructor_WhenSomeEntriesHaveNoStoredRank_ShouldRankAllEntries()
    {
        var earlier = CreateParticipation(1, START.AddHours(1), present: START.AddHours(1).AddMinutes(5));
        var later = CreateParticipation(2, START.AddHours(2), present: START.AddHours(2).AddMinutes(5));
        var ranking = CreateRanking([new RankingEntry(later, 1, false), new RankingEntry(earlier, null, false)]);

        var result = new Result(ranking);

        Assert.Equal([earlier.Id, later.Id], result.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], result.Entries.Select(x => x.Rank));
    }

    static Ranking CreateRanking(IEnumerable<RankingEntry> entries)
    {
        return new Ranking(
            "Competition",
            CompetitionRuleset.FEI,
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

    static Participation CreateParticipation(int number, DateTimeOffset? arrive, DateTimeOffset? present)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, number);
        var horse = new Horse($"Horse {number}", null, null, number);
        var combination = new Combination(number, athlete, horse, null, "20", null, null, number);

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.FEI),
            combination,
            new PhaseCollection([CreatePhase(START, arrive, present)]),
            null,
            eventId: 10,
            id: number
        );
    }

    static Phase CreatePhase(DateTimeOffset start, DateTimeOffset? arrive, DateTimeOffset? present)
    {
        return new Phase(
            "GATE1",
            20,
            40,
            null,
            CompetitionRuleset.FEI,
            true,
            null,
            Timestamp.Create(start),
            arrive == null ? null : Timestamp.Create(arrive.Value),
            present == null ? null : Timestamp.Create(present.Value),
            null,
            false,
            false,
            false
        );
    }
}
