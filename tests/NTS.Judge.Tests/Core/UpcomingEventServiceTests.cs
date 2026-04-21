using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain.Abstractions;
using Not.Exceptions;
using Not.Notify;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;
using NTS.Judge.Features.Setup.UpcomingEvents;
using NTS.Judge.Tests.Core.Implementations;
using SetupOfficial = NTS.Domain.Setup.Aggregates.UpcomingEvents.Official;
using SetupParticipation = NTS.Domain.Setup.Aggregates.UpcomingEvents.Participation;

namespace NTS.Judge.Tests.Core;

public class UpcomingEventServiceTests
{
    [Fact]
    public async Task Validate_WhenUpcomingEventIdTargetsInvalidEvent_ReturnsIssuesForThatEvent()
    {
        var service = CreateService([CreateValidEvent(1), CreateInvalidEvent(2)]);

        var result = await service.Validate(2);

        Assert.NotEmpty(result.Data ?? []);
    }

    [Fact]
    public async Task Validate_WhenUpcomingEventIdMissing_ThrowsGuardException()
    {
        var service = CreateService([CreateValidEvent(1)]);

        var exception = await Assert.ThrowsAsync<GuardException>(() => service.Validate(42));

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
        var service = new UpcomingEventService(repository, new TestNotifier());

        await service.DeleteParticipation(2, 101, 1);

        Assert.Single(repository.Items.Single(x => x.Id == 1).Competitions.Single().Participations);
        Assert.Empty(repository.Items.Single(x => x.Id == 2).Competitions.Single().Participations);
    }

    static UpcomingEventService CreateService(IEnumerable<UpcomingEvent> upcomingEvents)
    {
        return new UpcomingEventService(new RecordingRepository<UpcomingEvent>(upcomingEvents), new TestNotifier());
    }

    static Country CreateCountry()
    {
        return new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
    }

    static UpcomingEvent CreateValidEvent(int id, int? competitionId = null, int? participationNumber = null)
    {
        var country = CreateCountry();
        var athlete = new Athlete(new Person(["John", "Doe"]), null, country, null, id * 10 + 1);
        var horse = new Horse($"Horse {id}", null, id * 10 + 1);
        var combination = new Combination(participationNumber ?? id * 100 + 1, athlete, horse, id * 10 + 1);
        var loop = new Loop(40, id * 10 + 1);
        var phase = new Phase(loop, 40, null, id * 10 + 1);
        var participation = CreateParticipation(combination, id * 10 + 1);
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
        var official = new SetupOfficial(new Person(["Judge", $"{id}"]), OfficialRole.GroundJuryPresident, id * 10 + 1);

        return CreateEvent(id, country, [competition], [official], [loop], [combination]);
    }

    static UpcomingEvent CreateEvent(
        int id,
        Country? country = null,
        IReadOnlyCollection<Competition>? competitions = null,
        IReadOnlyCollection<SetupOfficial>? officials = null,
        IReadOnlyCollection<Loop>? loops = null,
        IReadOnlyCollection<Combination>? combinations = null
    )
    {
        return new UpcomingEvent(
            $"Event {id}",
            "Sofia",
            country ?? CreateCountry(),
            null,
            null,
            null,
            competitions ?? [],
            officials ?? [],
            loops ?? [],
            combinations ?? [],
            id
        );
    }

    static SetupParticipation CreateParticipation(int id)
    {
        var country = CreateCountry();
        var combination = new Combination(
            number: id,
            athlete: new Athlete(new Person(["John", "Doe"]), null, country, null, id * 10 + 1),
            horse: new Horse($"Horse {id}", null, id * 10 + 2),
            id: id * 10 + 3
        );
        return CreateParticipation(combination, id);
    }

    static SetupParticipation CreateParticipation(Combination combination, int id)
    {
        return new SetupParticipation(
            isNotRanked: false,
            combination: combination,
            category: ParticipationCategory.Senior,
            startTimeOverride: null,
            maxSpeedOverride: null,
            minSpeedOverride: null,
            id: id
        );
    }

    static UpcomingEvent CreateInvalidEvent(int id)
    {
        var country = CreateCountry();
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
            .Select(
                (x, index) =>
                    new Phase(new Loop(x.Distance, id * 10 + index + 1), x.Recovery, x.RestMinutes, id * 10 + index + 1)
            )
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
