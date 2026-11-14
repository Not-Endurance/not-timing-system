using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Core.Objects.Arrivelists;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Core.Tests.Objects.Arrivelists;

public sealed class ArrivelistTests
{
    static readonly DateTimeOffset Yesterday = DateTimeOffset.Now.AddDays(-1);
    static readonly DateTimeOffset Start = new DateTimeOffset(Yesterday.Date.AddHours(10), Yesterday.Offset);

    [Fact]
    public void Entries_WhenParticipationsAreInMixedStates_ShouldIncludeOnlyStartedUnarrivedActive()
    {
        var now = DateTimeOffset.Now;
        var onCourse = CreateParticipation(1, [CreatePhase(20, now.AddMinutes(-30), isFinal: true)]);
        var future = CreateParticipation(2, [CreatePhase(20, now.AddMinutes(30), isFinal: true)]);
        var arrived = CreateParticipation(3, [CreatePhase(20, now.AddMinutes(-30), now.AddMinutes(-5), isFinal: true)]);
        var complete = CreateParticipation(
            4,
            [CreatePhase(20, now.AddMinutes(-30), now.AddMinutes(-5), now, isFinal: true)]
        );
        var eliminated = CreateParticipation(
            5,
            [CreatePhase(20, now.AddMinutes(-30), isFinal: true)],
            eliminated: new Retired()
        );

        var arrivelist = new Arrivelist([onCourse, future, arrived, complete, eliminated]);

        Assert.Equal([1], arrivelist.Entries.Select(x => x.Number));
        var entry = Assert.Single(arrivelist.Entries);
        Assert.Equal("Athlete 1", entry.AthleteName);
        Assert.Equal("Horse 1", entry.HorseName);
    }

    [Fact]
    public void Entries_WhenPreviousPhasesAreCompleted_ShouldEstimateArrivalsFromWholeJourney()
    {
        var day = DateTimeOffset.Now.AddDays(-1).Date;
        var completedStart = new DateTimeOffset(day.AddHours(8), DateTimeOffset.Now.Offset);
        var completedArrive = new DateTimeOffset(day.AddHours(9), DateTimeOffset.Now.Offset);
        var completedPresent = new DateTimeOffset(day.AddHours(9).AddMinutes(10), DateTimeOffset.Now.Offset);
        var currentStart = new DateTimeOffset(day.AddHours(10), DateTimeOffset.Now.Offset);
        var participation = CreateParticipation(
            10,
            [
                CreatePhase(20, completedStart, completedArrive, completedPresent),
                CreatePhase(20, currentStart, isFinal: true),
            ],
            PhaseAverage(min: 10, max: 20)
        );

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Equal(currentStart.AddMinutes(50), entry.Fast!.ToDateTimeOffset());
        Assert.Equal(currentStart.AddMinutes(70), entry.Average!.ToDateTimeOffset());
        Assert.Equal(currentStart.AddMinutes(170), entry.Slow!.ToDateTimeOffset());
    }

    [Fact]
    public void Entries_WhenNoCompletedDistance_ShouldOmitAverageEstimate()
    {
        var start = DateTimeOffset.Now.AddMinutes(-30);
        var participation = CreateParticipation(
            20,
            [CreatePhase(20, start, isFinal: true)],
            PhaseAverage(min: 10, max: 20)
        );

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.NotNull(entry.Fast);
        Assert.Null(entry.Average);
        Assert.NotNull(entry.Slow);
    }

    [Fact]
    public void Entries_WhenEstimatesAreAvailableForSomeEntries_ShouldSortByFirstAvailableEstimate()
    {
        var day = DateTimeOffset.Now.AddDays(-1).Date;
        var currentStart = new DateTimeOffset(day.AddHours(10), DateTimeOffset.Now.Offset);
        var slowestOnly = CreateParticipation(1, [CreatePhase(10, currentStart, isFinal: true)], PhaseAverage(min: 20));
        var averageOnly = CreateParticipation(
            2,
            [
                CreatePhase(
                    30,
                    new DateTimeOffset(day.AddHours(8), DateTimeOffset.Now.Offset),
                    new DateTimeOffset(day.AddHours(9), DateTimeOffset.Now.Offset),
                    new DateTimeOffset(day.AddHours(9), DateTimeOffset.Now.Offset)
                ),
                CreatePhase(10, currentStart, isFinal: true),
            ]
        );
        var fastestOnly = CreateParticipation(3, [CreatePhase(10, currentStart, isFinal: true)], PhaseAverage(max: 15));

        var arrivelist = new Arrivelist([fastestOnly, slowestOnly, averageOnly]);

        Assert.Equal([2, 1, 3], arrivelist.Entries.Select(x => x.Number));
    }

    [Fact]
    public void Entries_WhenJourneyStartedBeforeCurrentPhase_ShouldNotClampEstimateToPhaseStart()
    {
        var day = DateTimeOffset.Now.AddDays(-1).Date;
        var completedStart = new DateTimeOffset(day.AddHours(6), DateTimeOffset.Now.Offset);
        var completedArrive = new DateTimeOffset(day.AddHours(10), DateTimeOffset.Now.Offset);
        var currentStart = new DateTimeOffset(day.AddHours(12), DateTimeOffset.Now.Offset);
        var participation = CreateParticipation(
            30,
            [
                CreatePhase(20, completedStart, completedArrive, completedArrive),
                CreatePhase(20, currentStart, isFinal: true),
            ],
            PhaseAverage(max: 20)
        );

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Equal(currentStart.AddHours(-2), entry.Fast!.ToDateTimeOffset());
        Assert.True(entry.Fast.ToDateTimeOffset() < currentStart);
    }

    [Fact]
    public void Entries_WhenLoopSpeedIsCapped_ShouldNotDependOnRecovery()
    {
        var participation = CreateParticipation(1, [CreatePhase(20, Start)], Loop(max: 20));

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Equal(Start.AddMinutes(60), entry.Fast!.ToDateTimeOffset());
    }

    [Fact]
    public void Entries_WhenPhaseSpeedIsCapped_ShouldAllowForMaximumRecoveryAtEarliest()
    {
        var participation = CreateParticipation(1, [CreatePhase(20, Start, maxRecovery: 40)], Phase(max: 20));

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Equal(Start.AddMinutes(20), entry.Fast!.ToDateTimeOffset());
    }

    [Fact]
    public void Entries_WhenPhaseSpeedIsMinimum_ShouldAllowNoRecoveryAtLatest()
    {
        var participation = CreateParticipation(1, [CreatePhase(20, Start, maxRecovery: 40)], Phase(min: 10));

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Equal(Start.AddMinutes(120), entry.Slow!.ToDateTimeOffset());
    }

    [Fact]
    public void Entries_WhenFinalPhaseHasPhaseSpeedCap_ShouldIgnoreRecovery()
    {
        var participation = CreateParticipation(1, [CreatePhase(20, Start, isFinal: true)], Phase(max: 20));

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Equal(Start.AddMinutes(60), entry.Fast!.ToDateTimeOffset());
    }

    [Fact]
    public void Entries_WhenSeveralCapsApply_ShouldUseLatestOfEarliestBoundsAndEarliestOfLatestBounds()
    {
        var participation = CreateParticipation(
            1,
            [CreatePhase(20, Start, maxRecovery: 40)],
            new SpeedRestrictions(10, 20, 15, 20, null, null, null, null)
        );

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Equal(Start.AddMinutes(60), entry.Fast!.ToDateTimeOffset());
        Assert.Equal(Start.AddMinutes(80), entry.Slow!.ToDateTimeOffset());
    }

    [Fact]
    public void Entries_WhenLoopAverageIsCapped_ShouldUseTimeOfCompletedLoops()
    {
        var previousStart = Start.AddHours(-2);
        var previousArrive = previousStart.AddMinutes(60);
        var participation = CreateParticipation(
            1,
            [CreatePhase(20, previousStart, previousArrive, previousArrive), CreatePhase(20, Start, isFinal: true)],
            new SpeedRestrictions(null, null, null, null, null, 15, null, null)
        );

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Equal(Start.AddMinutes(100), entry.Fast!.ToDateTimeOffset());
    }

    [Fact]
    public void Entries_WhenLimitIsZero_ShouldIgnoreItInsteadOfNeverArriving()
    {
        var participation = CreateParticipation(1, [CreatePhase(20, Start, isFinal: true)], Loop(min: 0, max: 0));

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Null(entry.Fast);
        Assert.Null(entry.Slow);
    }

    static SpeedRestrictions PhaseAverage(double? min = null, double? max = null)
    {
        return new SpeedRestrictions(null, null, null, null, null, null, min, max);
    }

    static SpeedRestrictions Loop(double? min = null, double? max = null)
    {
        return new SpeedRestrictions(min, max, null, null, null, null, null, null);
    }

    static SpeedRestrictions Phase(double? min = null, double? max = null)
    {
        return new SpeedRestrictions(null, null, min, max, null, null, null, null);
    }

    static Participation CreateParticipation(
        int number,
        IEnumerable<Phase> phases,
        SpeedRestrictions? restrictions = null,
        Eliminated? eliminated = null
    )
    {
        var phaseList = phases.ToList();
        var country = new Country(number, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, number);
        var horse = new Horse($"Horse {number}", null, null, number);
        var totalDistance = phaseList.Sum(x => x.Length);
        var combination = new Combination(number, athlete, horse, null, $"{totalDistance:0.##}", restrictions, number);

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", false),
            combination,
            new PhaseCollection(phaseList),
            eliminated,
            eventId: 1
        );
    }

    static Phase CreatePhase(
        double length,
        DateTimeOffset? start,
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        bool isFinal = false,
        int maxRecovery = 40
    )
    {
        return new Phase(
            "",
            length,
            maxRecovery,
            isFinal ? null : 40,
            isFinal,
            null,
            Timestamp.Create(start),
            Timestamp.Create(arrive),
            Timestamp.Create(present),
            null,
            false,
            false,
            false
        );
    }
}
