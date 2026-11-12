using System.Linq.Expressions;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Startlists;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Core.Objects.Payloads;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;

namespace NoTiming.Application.Tests.Startlists;

public sealed class StartlistServiceTests
{
    [Fact]
    public async Task Handle_WhenParticipationEliminated_ShouldRefreshFromRepositoryAndExcludeFromUpcoming()
    {
        var payloadParticipation = CreateParticipationWithHistoryAndFutureStart(301);
        var eliminatedParticipation = CreateParticipationWithHistoryAndFutureStart(301, new Withdrawn());
        var repository = new FakeParticipationRepository(eliminatedParticipation);
        var service = new StartlistService(repository);

        await service.Handle(new ParticipationEliminated(payloadParticipation), CancellationToken.None);

        Assert.DoesNotContain(service.Upcoming, x => x.Number == 301);
        Assert.Contains(service.History, x => x.Number == 301);
    }

    [Fact]
    public async Task Handle_WhenParticipationRestored_ShouldReturnParticipationToUpcoming()
    {
        var payloadParticipation = CreateParticipationWithHistoryAndFutureStart(301);
        var eliminatedParticipation = CreateParticipationWithHistoryAndFutureStart(301, new Withdrawn());
        var restoredParticipation = CreateParticipationWithHistoryAndFutureStart(301);
        var repository = new FakeParticipationRepository(eliminatedParticipation);
        var service = new StartlistService(repository);
        await service.Handle(new ParticipationEliminated(payloadParticipation), CancellationToken.None);

        await repository.Update(restoredParticipation);
        await service.Handle(new ParticipationRestored(eliminatedParticipation), CancellationToken.None);

        Assert.Contains(service.Upcoming, x => x.Number == 301);
    }

    static Participation CreateParticipationWithHistoryAndFutureStart(int number, Eliminated? eliminated = null)
    {
        var now = DateTimeOffset.Now;
        var firstStart = now.AddHours(-2);
        var firstArrive = firstStart.AddHours(1);
        var firstPresent = firstArrive.AddMinutes(5);
        var phases = new[]
        {
            CreatePhase(firstStart, firstArrive, firstPresent),
            CreatePhase(now.AddMinutes(30), isFinal: true),
        };
        var country = new Country(number, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, number);
        var horse = new Horse($"Horse {number}", null, null, number);
        var totalDistance = phases.Sum(x => x.Length);
        var combination = new Combination(number, athlete, horse, null, $"{totalDistance:0.##}", null, null, number);

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", false),
            combination,
            new PhaseCollection(phases),
            eliminated,
            eventId: 1,
            id: number
        );
    }

    static Phase CreatePhase(
        DateTimeOffset? start = null,
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        bool isFinal = false
    )
    {
        return new Phase(
            "",
            20,
            40,
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

    sealed class FakeParticipationRepository : IEventScopedRepository<Participation>
    {
        readonly List<Participation> _items;

        public FakeParticipationRepository(params Participation[] items)
        {
            _items = items.ToList();
        }

        public Task Create(Participation item)
        {
            _items.Add(item);
            return Task.CompletedTask;
        }

        public Task<Participation?> Read(int id)
        {
            return Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        }

        public Task<Participation?> Read(Expression<Func<Participation, bool>> filter)
        {
            return Task.FromResult(_items.AsQueryable().FirstOrDefault(filter));
        }

        public Task<IEnumerable<Participation>> ReadMany()
        {
            return Task.FromResult<IEnumerable<Participation>>(_items);
        }

        public Task<IEnumerable<Participation>> ReadMany(Expression<Func<Participation, bool>> filter)
        {
            return Task.FromResult<IEnumerable<Participation>>(_items.AsQueryable().Where(filter).ToArray());
        }

        public Task Update(Participation item)
        {
            var index = _items.FindIndex(x => x.Id == item.Id);
            if (index < 0)
            {
                _items.Add(item);
            }
            else
            {
                _items[index] = item;
            }

            return Task.CompletedTask;
        }

        public Task Delete(Participation item)
        {
            _items.RemoveAll(x => x.Id == item.Id);
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<Participation> items)
        {
            var ids = items.Select(x => x.Id).ToHashSet();
            _items.RemoveAll(x => ids.Contains(x.Id));
            return Task.CompletedTask;
        }

        public Task DeleteMany(Expression<Func<Participation, bool>> filter)
        {
            var predicate = filter.Compile();
            _items.RemoveAll(x => predicate(x));
            return Task.CompletedTask;
        }
    }
}
