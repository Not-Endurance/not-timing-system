using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Arrivelists;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

public sealed class ArrivelistTests
{
    [Fact]
    public void Entries_include_only_started_unarrived_active_participations()
    {
        var now = DateTimeOffset.Now;
        var onCourse = CreateParticipation(1, [CreatePhase(20, now.AddMinutes(-30), isFinal: true)]);
        var future = CreateParticipation(2, [CreatePhase(20, now.AddMinutes(30), isFinal: true)]);
        var arrived = CreateParticipation(
            3,
            [CreatePhase(20, now.AddMinutes(-30), now.AddMinutes(-5), isFinal: true)]
        );
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
    public void Entries_estimate_arrivals_from_the_whole_journey_so_far()
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
            minAverageSpeed: 10,
            maxAverageSpeed: 20
        );

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Equal(currentStart.AddMinutes(50), entry.Fast!.ToDateTimeOffset());
        Assert.Equal(currentStart.AddMinutes(70), entry.Average!.ToDateTimeOffset());
        Assert.Equal(currentStart.AddMinutes(170), entry.Slow!.ToDateTimeOffset());
    }

    [Fact]
    public void Average_estimate_is_missing_without_completed_distance()
    {
        var start = DateTimeOffset.Now.AddMinutes(-30);
        var participation = CreateParticipation(
            20,
            [CreatePhase(20, start, isFinal: true)],
            minAverageSpeed: 10,
            maxAverageSpeed: 20
        );

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.NotNull(entry.Fast);
        Assert.Null(entry.Average);
        Assert.NotNull(entry.Slow);
    }

    [Fact]
    public void Entries_sort_by_first_available_estimate()
    {
        var day = DateTimeOffset.Now.AddDays(-1).Date;
        var currentStart = new DateTimeOffset(day.AddHours(10), DateTimeOffset.Now.Offset);
        var slowestOnly = CreateParticipation(
            1,
            [CreatePhase(10, currentStart, isFinal: true)],
            minAverageSpeed: 20
        );
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
        var fastestOnly = CreateParticipation(
            3,
            [CreatePhase(10, currentStart, isFinal: true)],
            maxAverageSpeed: 15
        );

        var arrivelist = new Arrivelist([fastestOnly, slowestOnly, averageOnly]);

        Assert.Equal([2, 1, 3], arrivelist.Entries.Select(x => x.Number));
    }

    [Fact]
    public void Estimates_are_not_clamped_to_current_phase_start()
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
            maxAverageSpeed: 20
        );

        var entry = Assert.Single(new Arrivelist([participation]).Entries);

        Assert.Equal(currentStart.AddHours(-2), entry.Fast!.ToDateTimeOffset());
        Assert.True(entry.Fast.ToDateTimeOffset() < currentStart);
    }

    static Participation CreateParticipation(
        int number,
        IEnumerable<Phase> phases,
        double? minAverageSpeed = null,
        double? maxAverageSpeed = null,
        Eliminated? eliminated = null
    )
    {
        var phaseList = phases.ToList();
        var country = new Country(number, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, number);
        var horse = new Horse($"Horse {number}", null, null, number);
        var totalDistance = phaseList.Sum(x => x.Length);
        var combination = new Combination(
            number,
            athlete,
            horse,
            null,
            $"{totalDistance:0.##}",
            minAverageSpeed,
            maxAverageSpeed,
            number
        );

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
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
        bool isFinal = false
    )
    {
        return new Phase(
            "",
            length,
            40,
            isFinal ? null : 40,
            CompetitionRuleset.Regional,
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
