using Not.Application.CRUD.Ports;
using NTS.Application.Factories;
using NTS.Application.Startlists;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using SetupCombination = NTS.Domain.Setup.Aggregates.UpcomingEvents.Combination;
using SetupCompetition = NTS.Domain.Setup.Aggregates.UpcomingEvents.Competition;
using SetupLoop = NTS.Domain.Setup.Aggregates.UpcomingEvents.Loop;
using SetupPhase = NTS.Domain.Setup.Aggregates.UpcomingEvents.Phase;
using SetupParticipation = NTS.Domain.Setup.Aggregates.UpcomingEvents.Participation;

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
        var athlete = new Athlete(new Person([$"Rider{id}", "Test"]), null, country, null, 2000 + id);
        var horse = new Horse($"Horse{id}", null, 3000 + id);
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

    static DateTimeOffset CreateFutureUtcTimestamp()
    {
        var now = DateTimeOffset.Now;
        var remaining = TimeSpan.FromDays(1) - now.TimeOfDay - TimeSpan.FromSeconds(1);
        var lead =
            remaining > TimeSpan.FromMinutes(30)
                ? TimeSpan.FromMinutes(30)
                : remaining > TimeSpan.FromMinutes(1)
                    ? TimeSpan.FromMinutes(1)
                    : TimeSpan.FromSeconds(5);
        return CreateUtcTimestamp(now.TimeOfDay + lead);
    }

    static DateTimeOffset CreatePastUtcTimestamp()
    {
        var now = DateTimeOffset.Now;
        var lead =
            now.TimeOfDay > TimeSpan.FromMinutes(30)
                ? TimeSpan.FromMinutes(30)
                : now.TimeOfDay > TimeSpan.FromMinutes(1)
                    ? TimeSpan.FromMinutes(1)
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

    sealed class MutableParticipationRepository : IReadMany<Participation>
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

        public Task<IEnumerable<Participation>> ReadMany(System.Linq.Expressions.Expression<Func<Participation, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<Participation>>(Items.Where(predicate).ToList());
        }
    }
}
