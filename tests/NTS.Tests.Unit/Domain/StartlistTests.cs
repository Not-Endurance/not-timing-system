using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

public sealed class StartlistTests
{
    [Fact]
    public void Upcoming_keeps_multiple_stages_in_one_ordered_list()
    {
        var starts = CreateUpcomingStarts();
        var startlist = new Startlist(
            [
                CreateParticipation(101, starts[0], phaseNumber: 2),
                CreateParticipation(102, starts[1], phaseNumber: 1),
                CreateParticipation(103, starts[2], phaseNumber: 2),
            ]
        );

        Assert.Equal([101, 102, 103], startlist.Upcoming.Select(x => x.Number));
        Assert.Equal([2, 1, 2], startlist.Upcoming.Select(x => x.PhaseNumber));
        Assert.Equal(["GATE2/40", "GATE1/20", "GATE2/40"], startlist.Upcoming.Select(x => x.Gate));
    }

    static DateTimeOffset[] CreateUpcomingStarts()
    {
        var now = DateTimeOffset.Now;
        var latestBaseTime = new TimeSpan(23, 40, 0);
        var baseTime = now.TimeOfDay.Add(TimeSpan.FromMinutes(20));
        if (baseTime > latestBaseTime)
        {
            baseTime = latestBaseTime;
        }

        var baseStart = new DateTimeOffset(now.Date.Add(baseTime), now.Offset);
        return [baseStart, baseStart.AddMinutes(1), baseStart.AddMinutes(2)];
    }

    static Participation CreateParticipation(int number, DateTimeOffset start, int phaseNumber)
    {
        var phases =
            phaseNumber == 1
                ? new[] { CreatePhase(start, isFinal: true) }
                : [CreatePhase(null), CreatePhase(start, isFinal: true)];
        var country = new Country(number, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, number);
        var horse = new Horse($"Horse {number}", null, null, number);
        var totalDistance = phases.Sum(x => x.Length);
        var combination = new Combination(number, athlete, horse, null, $"{totalDistance:0.##}", null, null, number);

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection(phases),
            null,
            eventId: 1
        );
    }

    static Phase CreatePhase(DateTimeOffset? start = null, bool isFinal = false)
    {
        return new Phase(
            "",
            20,
            40,
            isFinal ? null : 40,
            CompetitionRuleset.Regional,
            isFinal,
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
