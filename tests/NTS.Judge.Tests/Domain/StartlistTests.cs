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
        var first = CreateParticipation(1, 10, now.AddMinutes(-8), now.AddMinutes(-6));
        var second = CreateParticipation(2, 11, now.AddMinutes(-10), now.AddMinutes(-7));

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
        var upcoming = CreateParticipation(4, 21, now.AddMinutes(-10));

        var startlist = new Startlist([history, upcoming]);

        Assert.Equal([1], startlist.HistoryByStage.Keys.ToArray());
        Assert.Equal([20], startlist.HistoryByStage[1].Select(x => x.Number).ToArray());
        Assert.Equal([1], startlist.UpcomingByStage.Keys.ToArray());
        Assert.Equal([21], startlist.UpcomingByStage[1].Select(x => x.Number).ToArray());
    }

    [Fact]
    public void UpcomingByStage_WhenEntriesAreMixed_FutureStartsRemainAboveLateStarts()
    {
        var now = DateTimeOffset.Now;
        var late = CreateParticipation(30, 1, now.AddSeconds(-5));
        var futureA = CreateParticipation(31, 38, now.AddSeconds(30));
        var futureB = CreateParticipation(32, 80, now.AddSeconds(39));

        var startlist = new Startlist([late, futureA, futureB]);

        Assert.Equal([38, 80, 1], startlist.UpcomingByStage[1].Select(x => x.Number).ToArray());
        Assert.Equal(StartlistEntryState.Late, startlist.UpcomingByStage[1].Last().State);
    }

    [Fact]
    public void UpdateState_WhenFutureEntryBecomesLate_ReordersItBelowPendingEntries()
    {
        var now = DateTimeOffset.Now;
        var firstLate = CreateParticipation(33, 1, now.AddMinutes(-2));
        var pending = CreateParticipation(34, 38, now.AddMinutes(10));

        var startlist = new Startlist([pending, firstLate]);

        startlist.UpdateState();

        Assert.Equal([38, 1], startlist.UpcomingByStage[1].Select(x => x.Number).ToArray());
        Assert.Equal(StartlistEntryState.Late, startlist.UpcomingByStage[1].Last().State);
        Assert.Equal(StartlistEntryState.Resting, startlist.UpcomingByStage[1].First().State);
    }

    [Fact]
    public void UpcomingByStage_WhenRemovingEntries_DoesNotLeaveStaleStageGroups()
    {
        var now = DateTimeOffset.Now;
        var first = CreateParticipation(5, 31, now.AddMinutes(-10));
        var second = CreateParticipation(6, 32, now.AddMinutes(-8));

        var startlist = new Startlist([first, second]);

        Assert.Equal(2, startlist.UpcomingByStage[1].Count);

        startlist.Remove(31);
        Assert.Equal([32], startlist.UpcomingByStage[1].Select(x => x.Number).ToArray());

        startlist.Remove(32);
        Assert.Empty(startlist.UpcomingByStage);
    }

    [Fact]
    public void UpsertNextPhase_WhenAddingFutureEntry_CreatesStageGroup()
    {
        var startlist = new Startlist([]);
        var now = DateTimeOffset.Now;
        var participation = CreateParticipation(7, 50, now.AddMinutes(-8), now.AddMinutes(-5));

        startlist.UpsertNext(participation);

        Assert.Equal([2], startlist.UpcomingByStage.Keys.ToArray());
        Assert.Equal([50], startlist.UpcomingByStage[2].Select(x => x.Number).ToArray());
    }

    [Fact]
    public void UpsertNextPhase_WhenCurrentPhaseIsCompleteButNextPhaseHasNoStart_UsesOutTimeForNextPhase()
    {
        var now = DateTimeOffset.Now;
        var participation = CreateParticipation(
            8,
            60,
            phase1Start: now.AddMinutes(-40),
            phase2Start: null,
            phase1Arrive: now.AddMinutes(-10),
            phase1Present: now.AddMinutes(-5),
            includePhase2: true
        );
        var expectedStart = participation.Phases[0].GetOutTime();
        var startlist = new Startlist([]);

        startlist.UpsertNext(participation);

        var upcoming = Assert.Single(startlist.UpcomingByStage[2]);
        Assert.Equal(60, upcoming.Number);
        Assert.Equal(expectedStart, upcoming.Start);
    }

    [Fact]
    public void UpsertNextPhase_WhenSameParticipationIsAddedTwice_KeepsSingleUpcomingEntry()
    {
        var now = DateTimeOffset.Now;
        var participation = CreateParticipation(
            9,
            61,
            phase1Start: now.AddMinutes(-40),
            phase2Start: null,
            phase1Arrive: now.AddMinutes(-10),
            phase1Present: now.AddMinutes(-5),
            includePhase2: true
        );
        var startlist = new Startlist([]);

        startlist.UpsertNext(participation);
        startlist.UpsertNext(participation);

        Assert.Single(startlist.Upcoming);
        Assert.Single(startlist.UpcomingByStage[2]);
    }

    [Fact]
    public void UpsertCurrentPhase_WhenParticipationIsRestoredInCurrentPhase_AddsCurrentStage()
    {
        var startlist = new Startlist([]);
        var now = DateTimeOffset.Now;
        var participation = CreateParticipation(11, 63, now.AddMinutes(-8), now.AddMinutes(-5));

        startlist.UpsertCurrent(participation);

        var upcoming = Assert.Single(startlist.UpcomingByStage[1]);
        Assert.Equal(63, upcoming.Number);
        Assert.Equal(participation.Phases[0].StartTime, upcoming.Start);
    }

    [Fact]
    public void Remove_WhenParticipationHasMultipleUpcomingEntries_RemovesAllMatches()
    {
        var now = DateTimeOffset.Now;
        var participation = CreateParticipation(10, 62, now.AddMinutes(-8), now.AddMinutes(-5));
        var startlist = new Startlist([participation]);

        startlist.Remove(62);

        Assert.Empty(startlist.Upcoming);
        Assert.Empty(startlist.UpcomingByStage);
    }

    static Participation CreateParticipation(
        int id,
        int number,
        DateTimeOffset phase1Start,
        DateTimeOffset? phase2Start = null,
        DateTimeOffset? phase1Arrive = null,
        DateTimeOffset? phase1Present = null,
        bool includePhase2 = false
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
                isFinal: !includePhase2 && phase2Start == null,
                startTime: phase1Start,
                arriveTime: phase1Arrive,
                presentTime: phase1Present,
                id: 5000 + id * 10 + 1
            ),
        };

        if (includePhase2 || phase2Start != null)
        {
            phases.Add(
                CreatePhase(
                    length: 20,
                    maxRecovery: 40,
                    rest: null,
                    isFinal: true,
                    startTime: phase2Start,
                    arriveTime: null,
                    presentTime: null,
                    id: 5000 + id * 10 + 2
                )
            );
        }

        return new Participation(
            category: ParticipationCategory.Senior,
            competition: new Competition(
                $"Competition{id}",
                CompetitionRuleset.Regional,
                CompetitionType.Qualification
            ),
            combination: combination,
            phases: new PhaseCollection(phases),
            notQualified: null,
            eventId: 7000 + id,
            id: 6000 + id
        );
    }

    static Phase CreatePhase(
        double length,
        int maxRecovery,
        int? rest,
        bool isFinal,
        DateTimeOffset? startTime,
        DateTimeOffset? arriveTime,
        DateTimeOffset? presentTime,
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
            arriveTime: Timestamp.Create(arriveTime),
            presentTime: Timestamp.Create(presentTime),
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: id
        );
    }
}
