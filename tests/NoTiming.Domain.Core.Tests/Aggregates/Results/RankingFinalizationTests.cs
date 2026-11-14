using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Core.Aggregates.Results;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Core.Tests.Aggregates.Results;

public sealed class RankingFinalizationTests
{
    static readonly DateTimeOffset START = new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PlaceEntries_WhenOnlyOneEntry_ShouldPlaceItFirst()
    {
        var ranking = CreateRanking([new RankingEntry(CreateParticipation(1, START.AddMinutes(30)), null, false)]);

        var placings = RankingFinalization.PlaceEntries(ranking);

        Assert.Equal(1, placings[1]);
    }

    [Fact]
    public void PlaceEntries_WhenSomeEntriesAreStillOnCourse_ShouldPlaceThemAfterArrivedEntries()
    {
        var arrived = CreateParticipation(1, START.AddMinutes(30));
        var onCourse = CreateParticipation(2, null);
        var ranking = CreateRanking([new RankingEntry(onCourse, null, false), new RankingEntry(arrived, null, false)]);

        var placings = RankingFinalization.PlaceEntries(ranking);

        Assert.Equal(1, placings[1]);
        Assert.Equal(2, placings[2]);
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

    static Participation CreateParticipation(int number, DateTimeOffset? arrive)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, number);
        var horse = new Horse($"Horse {number}", null, null, number);
        var combination = new Combination(number, athlete, horse, null, "20", null, number);
        var phase = new Phase(
            "GATE1",
            20,
            40,
            null,
            true,
            null,
            Timestamp.Create(START),
            Timestamp.Create(arrive),
            Timestamp.Create(arrive),
            null,
            false,
            false,
            false
        );

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", true),
            combination,
            new PhaseCollection([phase]),
            null,
            eventId: 10,
            id: number
        );
    }
}
