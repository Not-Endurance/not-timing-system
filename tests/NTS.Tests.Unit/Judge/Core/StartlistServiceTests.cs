using System.Linq.Expressions;
using NTS.Application.Contracts.Core;
using NTS.Application.Factories;
using NTS.Application.Startlists;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using CoreAthlete = NTS.Domain.Core.Aggregates.Participations.Entities.Athlete;
using CoreCombination = NTS.Domain.Core.Aggregates.Participations.Entities.Combination;
using CoreCompetition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;
using CoreHorse = NTS.Domain.Core.Aggregates.Participations.Entities.Horse;
using CorePhase = NTS.Domain.Core.Aggregates.Participations.Entities.Phase;
using CorePhaseCollection = NTS.Domain.Core.Aggregates.Participations.Objects.PhaseCollection;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupCombination = NTS.Domain.Setup.Aggregates.UpcomingEvents.Combination;
using SetupCompetition = NTS.Domain.Setup.Aggregates.UpcomingEvents.Competition;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupLoop = NTS.Domain.Setup.Aggregates.UpcomingEvents.Loop;
using SetupParticipation = NTS.Domain.Setup.Aggregates.UpcomingEvents.Participation;
using SetupPhase = NTS.Domain.Setup.Aggregates.UpcomingEvents.Phase;

namespace NTS.Judge.Tests.Core;

public class StartlistServiceTests
{
    [Fact]
    public async Task Handle_WhenEventConnectedIsRaisedAfterParticipationsExist_ReloadsUpcomingEntries()
    {
        var futureStart = CreateFutureUtcTimestamp();
        var repository = new MutableParticipationRepository([]);
        var service = new StartlistService(repository);

        await service.Load();

        Assert.Empty(service.Upcoming);

        repository.Items = [CreateParticipation(1, 77, futureStart)];

        await service.Handle(new EventConnected(14), CancellationToken.None);

        Assert.Equal([77], service.Upcoming.Select(x => x.Number).ToArray());
    }

    [Fact]
    public async Task Handle_WhenStartedParticipationHasFutureStartTimeOverride_LoadsUpcomingEntryAtOverrideTime()
    {
        var competitionStart = CreatePastUtcTimestamp();
        var overrideStart = CreateFutureUtcTimestamp();
        var repository = new MutableParticipationRepository([]);
        var service = new StartlistService(repository);

        await service.Load();

        repository.Items = [CreateStartedParticipation(1, 77, competitionStart, overrideStart)];

        await service.Handle(new EventConnected(14), CancellationToken.None);

        var upcoming = Assert.Single(service.Upcoming);
        Assert.Equal(77, upcoming.Number);
        Assert.Equal(overrideStart.ToUniversalTime(), upcoming.Start.ToDateTimeOffset());
    }

    [Fact]
    public async Task Handle_WhenStartedParticipationHasNoOverride_UsesCompetitionStartForUpcomingEntry()
    {
        var competitionStart = CreateFutureUtcTimestamp();
        var repository = new MutableParticipationRepository([]);
        var service = new StartlistService(repository);

        await service.Load();

        repository.Items = [CreateStartedParticipation(2, 88, competitionStart, null)];

        await service.Handle(new EventConnected(14), CancellationToken.None);

        var upcoming = Assert.Single(service.Upcoming);
        Assert.Equal(88, upcoming.Number);
        Assert.Equal(competitionStart.ToUniversalTime(), upcoming.Start.ToDateTimeOffset());
    }

    [Fact]
    public async Task Handle_WhenParticipationRestoredAndCurrentPhaseIsActive_AddsCurrentPhaseEntry()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MutableParticipationRepository([]);
        var service = new StartlistService(repository);
        var participation = CreateCoreParticipation(3, 99, now.AddMinutes(-5), now.AddMinutes(5), null, null);

        await service.Load();
        await service.Handle(new ParticipationRestored(participation), CancellationToken.None);

        var upcoming = Assert.Single(service.Upcoming);
        Assert.Equal(99, upcoming.Number);
        Assert.Equal(1, upcoming.PhaseNumber);
        Assert.Equal(participation.Phases.Current.StartTime!.ToDateTimeOffset(), upcoming.Start.ToDateTimeOffset());
    }

    [Fact]
    public async Task Handle_WhenParticipationRestoredAndCurrentPhaseIsComplete_WaitsForPhaseCompletedToAddNextPhase()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MutableParticipationRepository([]);
        var service = new StartlistService(repository);
        var participation = CreateCoreParticipation(
            4,
            100,
            now.AddMinutes(-45),
            null,
            now.AddMinutes(-10),
            now.AddMinutes(-5)
        );

        await service.Load();
        await service.Handle(new ParticipationRestored(participation), CancellationToken.None);

        Assert.Empty(service.Upcoming);

        await service.Handle(new PhaseCompleted(participation), CancellationToken.None);

        var upcoming = Assert.Single(service.Upcoming);
        Assert.Equal(100, upcoming.Number);
        Assert.Equal(2, upcoming.PhaseNumber);
        Assert.Equal(participation.Phases.Current.GetOutTime()!.ToDateTimeOffset(), upcoming.Start.ToDateTimeOffset());
    }

    [Fact]
    public async Task Handle_WhenFinalPhaseCompletes_KeepsStartedPhasesInHistory()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MutableParticipationRepository([]);
        var service = new StartlistService(repository);
        var participation = CreateCoreParticipation(
            5,
            101,
            now.AddMinutes(-20),
            now.AddMinutes(-16),
            now.AddMinutes(-18),
            now.AddMinutes(-17),
            now.AddMinutes(-14),
            now.AddMinutes(-13)
        );

        await service.Load();
        await service.Handle(new PhaseCompleted(participation), CancellationToken.None);

        Assert.Equal([1, 2], service.HistoryByStage.Keys.ToArray());
        Assert.Equal([101], service.HistoryByStage[1].Select(x => x.Number).ToArray());
        Assert.Equal([101], service.HistoryByStage[2].Select(x => x.Number).ToArray());
        Assert.Empty(service.Upcoming);
    }

    static Participation CreateParticipation(int id, int number, DateTimeOffset phase1Start)
    {
        return CreateStartedParticipation(id, number, phase1Start, phase1Start);
    }

    static Participation CreateStartedParticipation(
        int id,
        int number,
        DateTimeOffset competitionStart,
        DateTimeOffset? startTimeOverride
    )
    {
        var country = new Country(1000 + id, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new SetupAthlete(new Person([$"Rider{id}", "Test"]), null, country, null, 2000 + id);
        var horse = new SetupHorse($"Horse{id}", null, 3000 + id);
        var combination = new SetupCombination(number, athlete, horse, 4000 + id);
        var setupParticipation = new SetupParticipation(
            isNotRanked: false,
            combination: combination,
            category: ParticipationCategory.Senior,
            startTimeOverride: startTimeOverride,
            maxSpeedOverride: null,
            minSpeedOverride: null,
            id: 5000 + id
        );
        var setupCompetition = new SetupCompetition(
            $"Competition{id}",
            CompetitionType.Qualification,
            CompetitionRuleset.Regional,
            competitionStart,
            null,
            null,
            null,
            null,
            [new SetupPhase(new SetupLoop(20, 6000 + id), 40, null, 7000 + id)],
            [setupParticipation],
            8000 + id
        );

        return ParticipationAndRankingFactory.CreateParticipation(setupCompetition, setupParticipation, 9000 + id);
    }

    static Participation CreateCoreParticipation(
        int id,
        int number,
        DateTimeOffset phase1Start,
        DateTimeOffset? phase2Start,
        DateTimeOffset? phase1Arrive,
        DateTimeOffset? phase1Present,
        DateTimeOffset? phase2Arrive = null,
        DateTimeOffset? phase2Present = null
    )
    {
        var country = new Country(1000 + id, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new CoreAthlete(new Person([$"Rider{id}", "Test"]), country, null, null, 2000 + id);
        var horse = new CoreHorse($"Horse{id}", null, 3000 + id);
        var combination = new CoreCombination(number, athlete, horse, null, "40", null, null, 4000 + id);
        var phase1 = new CorePhase(
            gate: "",
            length: 20,
            maxRecovery: 40,
            rest: 30,
            ruleset: CompetitionRuleset.Regional,
            isFinal: false,
            compulsoryThresholdSpan: null,
            startTime: Timestamp.Create(phase1Start),
            arriveTime: Timestamp.Create(phase1Arrive),
            presentTime: Timestamp.Create(phase1Present),
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: 5000 + id * 10 + 1
        );
        var phase2 = new CorePhase(
            gate: "",
            length: 20,
            maxRecovery: 40,
            rest: null,
            ruleset: CompetitionRuleset.Regional,
            isFinal: true,
            compulsoryThresholdSpan: null,
            startTime: Timestamp.Create(phase2Start),
            arriveTime: Timestamp.Create(phase2Arrive),
            presentTime: Timestamp.Create(phase2Present),
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: 5000 + id * 10 + 2
        );

        return new Participation(
            category: ParticipationCategory.Senior,
            competition: new CoreCompetition(
                $"Competition{id}",
                CompetitionRuleset.Regional,
                CompetitionType.Qualification
            ),
            combination: combination,
            phases: new CorePhaseCollection([phase1, phase2]),
            notQualified: null,
            eventId: 9000 + id,
            id: 6000 + id
        );
    }

    static DateTimeOffset CreateFutureUtcTimestamp()
    {
        var now = DateTimeOffset.Now;
        var remaining = TimeSpan.FromDays(1) - now.TimeOfDay - TimeSpan.FromSeconds(1);
        var lead =
            remaining > TimeSpan.FromMinutes(30) ? TimeSpan.FromMinutes(30)
            : remaining > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1)
            : TimeSpan.FromSeconds(5);
        return CreateUtcTimestamp(now.TimeOfDay + lead);
    }

    static DateTimeOffset CreatePastUtcTimestamp()
    {
        var now = DateTimeOffset.Now;
        var lead =
            now.TimeOfDay > TimeSpan.FromMinutes(30) ? TimeSpan.FromMinutes(30)
            : now.TimeOfDay > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1)
            : TimeSpan.FromSeconds(5);
        return CreateUtcTimestamp(now.TimeOfDay - lead);
    }

    static DateTimeOffset CreateUtcTimestamp(TimeSpan timeOfDay)
    {
        var utcDate = DateTimeOffset.UtcNow.Date;
        if (timeOfDay < TimeSpan.Zero)
        {
            utcDate = utcDate.AddDays(-1);
            timeOfDay += TimeSpan.FromDays(1);
        }
        if (timeOfDay >= TimeSpan.FromDays(1))
        {
            utcDate = utcDate.AddDays(1);
            timeOfDay -= TimeSpan.FromDays(1);
        }
        return new DateTimeOffset(utcDate + timeOfDay, TimeSpan.Zero);
    }

    sealed class MutableParticipationRepository : IEventScopedRepository<Participation>
    {
        public MutableParticipationRepository(IEnumerable<Participation> items)
        {
            Items = items.ToList();
        }

        public List<Participation> Items { get; set; }

        public Task<IEnumerable<Participation>> ReadMany()
        {
            return Task.FromResult<IEnumerable<Participation>>(Items.ToList());
        }

        public Task<IEnumerable<Participation>> ReadMany(
            Expression<Func<Participation, bool>> filter
        )
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<Participation>>(Items.Where(predicate).ToList());
        }

        public Task Create(Participation item)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<Participation?> Read(int id)
        {
            return Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        }

        public Task<Participation?> Read(Expression<Func<Participation, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult(Items.FirstOrDefault(predicate));
        }

        public Task Update(Participation item)
        {
            Items.RemoveAll(x => x.Id == item.Id);
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task Delete(Participation item)
        {
            Items.RemoveAll(x => x.Id == item.Id);
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<Participation> items)
        {
            var ids = items.Select(x => x.Id).ToHashSet();
            Items.RemoveAll(x => ids.Contains(x.Id));
            return Task.CompletedTask;
        }

        public Task DeleteMany(Expression<Func<Participation, bool>> filter)
        {
            var predicate = filter.Compile();
            Items.RemoveAll(x => predicate(x));
            return Task.CompletedTask;
        }
    }
}
