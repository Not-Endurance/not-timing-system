using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Judge.Tests.Domain;

public class StartlistTests
{
    [Fact]
    public void UpcomingByStage_WhenConstructed_GroupsByPhaseAndOrdersByStart()
    {
        var now = DateTimeOffset.Now;
        var first = CreateParticipation(
            1,
            10,
            FutureOnSameDay(now, TimeSpan.FromMinutes(20)),
            FutureOnSameDay(now, TimeSpan.FromMinutes(40))
        );
        var second = CreateParticipation(
            2,
            11,
            FutureOnSameDay(now, TimeSpan.FromMinutes(10)),
            FutureOnSameDay(now, TimeSpan.FromMinutes(30))
        );

        var startlist = new Startlist([first, second]);

        Assert.Equal([1, 2], startlist.UpcomingByStage.Keys.ToArray());
        Assert.Equal([11, 10], startlist.UpcomingByStage[1].Select(x => x.Number).ToArray());
        Assert.Equal([11, 10], startlist.UpcomingByStage[2].Select(x => x.Number).ToArray());
    }

    [Fact]
    public void HistoryByStage_WhenConstructed_SeparatesPastFromUpcoming()
    {
        var now = DateTimeOffset.Now;
        var history = CreateParticipation(3, 20, now.AddMinutes(-20));
        var upcoming = CreateParticipation(4, 21, now.AddMinutes(10));

        var startlist = new Startlist([history, upcoming]);

        Assert.Equal([1], startlist.HistoryByStage.Keys.ToArray());
        Assert.Equal([20], startlist.HistoryByStage[1].Select(x => x.Number).ToArray());
        Assert.Equal([1], startlist.UpcomingByStage.Keys.ToArray());
        Assert.Equal([21], startlist.UpcomingByStage[1].Select(x => x.Number).ToArray());
    }

    [Fact]
    public void UpcomingByStage_WhenRemovingEntries_DoesNotLeaveStaleStageGroups()
    {
        var now = DateTimeOffset.Now;
        var first = CreateParticipation(5, 31, now.AddMinutes(10));
        var second = CreateParticipation(6, 32, now.AddMinutes(20));

        var startlist = new Startlist([first, second]);

        Assert.Equal(2, startlist.UpcomingByStage[1].Count);

        startlist.Remove(31);
        Assert.Equal([32], startlist.UpcomingByStage[1].Select(x => x.Number).ToArray());

        startlist.Remove(32);
        Assert.Empty(startlist.UpcomingByStage);
    }

    [Fact]
    public void Add_WhenAddingFutureEntry_CreatesStageGroup()
    {
        var startlist = new Startlist([]);
        var now = DateTimeOffset.Now;
        var participation = CreateParticipation(7, 50, now, FutureOnSameDay(now, TimeSpan.FromMinutes(30)));

        startlist.Add(participation);

        Assert.Equal([2], startlist.UpcomingByStage.Keys.ToArray());
        Assert.Equal([50], startlist.UpcomingByStage[2].Select(x => x.Number).ToArray());
    }

    static Participation CreateParticipation(
        int id,
        int number,
        DateTimeOffset phase1Start,
        DateTimeOffset? phase2Start = null
    )
    {
        var country = new Country(1000 + id, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person([$"Rider{id}", "Test"]), country, null, null, 2000 + id);
        var horse = new Horse($"Horse{id}", null, 3000 + id);
        var combination = new Combination(number, athlete, horse, null, "40", null, null, 4000 + id);

        var phases = new List<Phase>
        {
            CreatePhase(
                length: 20,
                maxRecovery: 40,
                rest: 30,
                isFinal: phase2Start == null,
                startTime: phase1Start,
                id: 5000 + id * 10 + 1
            ),
        };

        if (phase2Start != null)
        {
            phases.Add(
                CreatePhase(
                    length: 20,
                    maxRecovery: 40,
                    rest: null,
                    isFinal: true,
                    startTime: phase2Start.Value,
                    id: 5000 + id * 10 + 2
                )
            );
        }

        return new Participation(
            category: ParticipationCategory.Senior,
            competition: new Competition($"Competition{id}", CompetitionRuleset.Regional, CompetitionType.Qualification),
            combination: combination,
            phases: new PhaseCollection(phases),
            notQualified: null,
            id: 6000 + id
        );
    }

    static Phase CreatePhase(
        double length,
        int maxRecovery,
        int? rest,
        bool isFinal,
        DateTimeOffset startTime,
        int id
    )
    {
        return new Phase(
            gate: "",
            length: length,
            maxRecovery: maxRecovery,
            rest: rest,
            ruleset: CompetitionRuleset.Regional,
            isFinal: isFinal,
            compulsoryThresholdSpan: null,
            startTime: Timestamp.Create(startTime),
            arriveTime: null,
            presentTime: null,
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: id
        );
    }

    static DateTimeOffset FutureOnSameDay(DateTimeOffset now, TimeSpan preferredOffset)
    {
        var nextMidnight = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).AddDays(1);
        var maxOffset = nextMidnight - now - TimeSpan.FromMilliseconds(1);
        if (maxOffset <= TimeSpan.Zero)
        {
            return now;
        }
        var offset = preferredOffset <= maxOffset ? preferredOffset : maxOffset;
        return now + offset;
    }
}
