using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain.Abstractions;
using Not.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;
using NTS.Judge.Features;
using CoreOfficial = NTS.Domain.Core.Aggregates.Official;
using CoreParticipation = NTS.Domain.Core.Aggregates.Participation;
using SetupParticipation = NTS.Domain.Setup.Aggregates.UpcomingEvents.Participation;

namespace NTS.Judge.Tests.Core;

public class StartBusinessServiceTests
{
    [Fact]
    public async Task Validate_WhenUpcomingEventIdTargetsInvalidEvent_ReturnsIssuesForThatEvent()
    {
        var service = CreateService([CreateValidEvent(1), CreateInvalidEvent(2)]);

        var result = await service.Validate(2);

        Assert.NotEmpty(result.Data ?? []);
    }

    [Fact]
    public async Task Start_WhenUpcomingEventIdTargetsRequestedEvent_CreatesCoreEventWithSameId()
    {
        var events = new RecordingRepository<EnduranceEvent>();
        var officials = new RecordingRepository<CoreOfficial>();
        var participations = new RecordingRepository<CoreParticipation>();
        var rankings = new RecordingRepository<Ranking>();
        var service = CreateService([CreateValidEvent(1), CreateValidEvent(2)], events, officials, participations, rankings);

        var result = await service.CreateEnduranceEvent(2);

        Assert.Equal(2, result.Id);
        Assert.Equal(2, Assert.Single(events.CreatedItems).Id);
        Assert.Single(participations.CreatedItems);
        Assert.Single(rankings.CreatedItems);
        Assert.Empty(officials.CreatedItems);
    }

    [Fact]
    public async Task Start_WhenUpcomingEventIdMissing_ThrowsGuardException()
    {
        var service = CreateService([CreateValidEvent(1)]);

        var exception = await Assert.ThrowsAsync<GuardException>(() => service.CreateEnduranceEvent(42));

        Assert.Contains("42", exception.Message);
    }

    [Fact]
    public async Task DeleteParticipation_WhenUpcomingEventIdIsProvided_DeletesFromThatEventOnly()
    {
        var repository = new RecordingRepository<UpcomingEvent>(
            [
                CreateValidEvent(1, competitionId: 1, participationNumber: 101),
                CreateValidEvent(2, competitionId: 1, participationNumber: 101),
            ]
        );
        var service = new StartBusinessService(
            repository,
            new RecordingRepository<EnduranceEvent>(),
            new RecordingRepository<CoreOfficial>(),
            new RecordingRepository<CoreParticipation>(),
            new RecordingRepository<Ranking>()
        );

        await service.DeleteParticipation(2, 101, 1);

        Assert.Single(repository.Items.Single(x => x.Id == 1).Competitions.Single().Participations);
        Assert.Empty(repository.Items.Single(x => x.Id == 2).Competitions.Single().Participations);
    }

    static StartBusinessService CreateService(
        IEnumerable<UpcomingEvent> upcomingEvents,
        RecordingRepository<EnduranceEvent>? events = null,
        RecordingRepository<CoreOfficial>? officials = null,
        RecordingRepository<CoreParticipation>? participations = null,
        RecordingRepository<Ranking>? rankings = null
    )
    {
        return new StartBusinessService(
            new RecordingRepository<UpcomingEvent>(upcomingEvents),
            events ?? new RecordingRepository<EnduranceEvent>(),
            officials ?? new RecordingRepository<CoreOfficial>(),
            participations ?? new RecordingRepository<CoreParticipation>(),
            rankings ?? new RecordingRepository<Ranking>()
        );
    }

    static UpcomingEvent CreateValidEvent(int id, int? competitionId = null, int? participationNumber = null)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["John", "Doe"]), null, country, null, id * 10 + 1);
        var horse = new Horse($"Horse {id}", null, id * 10 + 1);
        var combination = new Combination(participationNumber ?? id * 100 + 1, athlete, horse, id * 10 + 1);
        var loop = new Loop(40, id * 10 + 1);
        var phase = new Phase(loop, 40, null, id * 10 + 1);
        var participation = new SetupParticipation(
            isNotRanked: false,
            combination: combination,
            category: ParticipationCategory.Senior,
            startTimeOverride: null,
            maxSpeedOverride: null,
            minSpeedOverride: null,
            id: id * 10 + 1
        );
        var competition = new Competition(
            $"Competition {id}",
            CompetitionType.Qualification,
            CompetitionRuleset.FEI,
            DateTimeOffset.UtcNow.AddHours(id),
            null,
            null,
            null,
            null,
            [phase],
            [participation],
            competitionId ?? id * 10 + 1
        );

        return new UpcomingEvent(
            $"Event {id}",
            "Sofia",
            country,
            null,
            null,
            null,
            [competition],
            [],
            [loop],
            [combination],
            id
        );
    }

    static UpcomingEvent CreateInvalidEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["John", "Doe"]), null, country, null, id * 10 + 1);
        var horse = new Horse($"Horse {id}", null, id * 10 + 1);
        var combination = new Combination(id * 100 + 1, athlete, horse, id * 10 + 1);

        var competitionOne = CreateCompetition(
            "Competition A",
            [(40d, 40, 30), (40d, 50, null)],
            combination,
            id * 10 + 1
        );
        var competitionTwo = CreateCompetition(
            "Competition B",
            [(35d, 40, 30), (45d, 50, null)],
            combination,
            id * 10 + 2
        );
        var loops = new List<Loop> { new(100, id * 100 + 1), new(101, id * 100 + 2) };

        return new UpcomingEvent(
            $"Event {id}",
            "Sofia",
            country,
            null,
            null,
            null,
            [competitionOne, competitionTwo],
            [],
            loops,
            [combination],
            id
        );
    }

    static Competition CreateCompetition(
        string name,
        IEnumerable<(double Distance, int Recovery, int? RestMinutes)> phaseConfig,
        Combination combination,
        int id
    )
    {
        var phases = phaseConfig
            .Select((x, index) => new Phase(new Loop(x.Distance, id * 10 + index + 1), x.Recovery, x.RestMinutes, id * 10 + index + 1))
            .ToList();
        var participation = new SetupParticipation(
            isNotRanked: false,
            combination: combination,
            category: ParticipationCategory.Senior,
            startTimeOverride: null,
            maxSpeedOverride: null,
            minSpeedOverride: null,
            id: id
        );

        return new Competition(
            name,
            CompetitionType.Qualification,
            CompetitionRuleset.FEI,
            DateTimeOffset.UtcNow.AddHours(id),
            null,
            null,
            null,
            null,
            phases,
            [participation],
            id
        );
    }

    sealed class RecordingRepository<T> : IRepository<T>
        where T : class, IEntity
    {
        readonly List<T> _items;

        public RecordingRepository(IEnumerable<T>? items = null)
        {
            _items = items?.ToList() ?? [];
        }

        public List<T> CreatedItems { get; } = [];
        public IReadOnlyList<T> Items => _items.AsReadOnly();

        public Task Create(T item)
        {
            CreatedItems.Add(item);
            _items.RemoveAll(x => x.Id == item.Id);
            _items.Add(item);
            return Task.CompletedTask;
        }

        public Task<T?> Read(Expression<Func<T, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult(_items.FirstOrDefault(predicate));
        }

        public Task<T?> Read(int id)
        {
            return Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        }

        public Task<IEnumerable<T>> ReadMany()
        {
            return Task.FromResult<IEnumerable<T>>(_items.ToList());
        }

        public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<T>>(_items.Where(predicate).ToList());
        }

        public Task Update(T item)
        {
            _items.RemoveAll(x => x.Id == item.Id);
            _items.Add(item);
            return Task.CompletedTask;
        }

        public Task Delete(T item)
        {
            _items.RemoveAll(x => x.Id == item.Id);
            return Task.CompletedTask;
        }

        public Task Delete(int id)
        {
            _items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task Delete(Expression<Func<T, bool>> filter)
        {
            var predicate = filter.Compile();
            _items.RemoveAll(x => predicate(x));
            return Task.CompletedTask;
        }

        public Task Delete(IEnumerable<T> items)
        {
            var ids = items.Select(x => x.Id).ToHashSet();
            _items.RemoveAll(x => ids.Contains(x.Id));
            return Task.CompletedTask;
        }
    }
}
