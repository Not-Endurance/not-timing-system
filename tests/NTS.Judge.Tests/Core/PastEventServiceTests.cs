using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Domain.Abstractions;
using NTS.Application.Core;
using NTS.Application.PastEvents;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Judge.Tests.Core;

public class PastEventServiceTests
{
    [Fact]
    public async Task LoadEvent_LoadsReadOnlyPastEventDataAndBuildsDocument()
    {
        var enduranceEvent = CreateEvent(14);
        var participation = CreateParticipation(number: 42, eventId: 14, participationId: 301, combinationId: 201);
        var firstRanking = CreateRanking("General Classification", participation, 601);
        var secondRanking = CreateRanking("Best Condition", participation, 602);
        var official = new Official(
            new Person(["Jane", "Doe"]),
            OfficialRole.GroundJuryPresident,
            eventId: 14,
            id: 701
        );
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IPastParticipationRepository>(new ParticipationPastRepository([participation]))
            .AddSingleton<IPastRankingRepository>(new RankingPastRepository([firstRanking, secondRanking]))
            .AddSingleton<IPastOfficialRepository>(new OfficialPastRepository([official]))
            .BuildServiceProvider();
        var service = new PastEventService(new EnduranceEventRepository([enduranceEvent]), serviceProvider);

        await service.LoadEvent(14);

        Assert.Equal(14, service.Event?.Id);
        Assert.Equal([firstRanking.Id, secondRanking.Id], service.Rankings.Select(x => x.Id));
        Assert.Equal(firstRanking.Id, service.CurrentRanking?.Id);
        Assert.NotNull(service.Document);
        Assert.Equal(firstRanking.Id, service.Document!.Ranklist.RankingId);
        Assert.Equal([1, 2], service.StartlistHistoryByStage.Keys.ToArray());

        service.Select(secondRanking);

        Assert.Equal(secondRanking.Id, service.CurrentRanking?.Id);
        Assert.Equal(secondRanking.Id, service.Document!.Ranklist.RankingId);
    }

    static EnduranceEvent CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            country,
            "Sofia",
            "Sofia",
            new EventSpan(DateTimeOffset.UtcNow.AddDays(-20), DateTimeOffset.UtcNow.AddDays(-18)),
            null,
            null,
            null,
            id
        );
    }

    static Ranking CreateRanking(string name, Participation participation, int id)
    {
        return new Ranking(
            name,
            CompetitionRuleset.FEI,
            CompetitionType.Qualification,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            [new RankingEntry(participation, 1, false, id + 1000)],
            participation.EventId,
            id
        );
    }

    static Participation CreateParticipation(int number, int eventId, int participationId, int combinationId)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["John", "Doe"]), country, null, null, 101);
        var horse = new Horse("Cassini", null, 102);
        var competition = new Competition("CEI 1*", CompetitionRuleset.FEI, CompetitionType.Qualification);
        var combination = new Combination(number, athlete, horse, null, "40", null, null, combinationId);
        var phase1 = new Phase(
            gate: "GATE1/40",
            length: 20,
            maxRecovery: 20,
            rest: 30,
            ruleset: CompetitionRuleset.FEI,
            isFinal: false,
            compulsoryThresholdSpan: null,
            startTime: Timestamp.Create(DateTimeOffset.UtcNow.AddHours(-3)),
            arriveTime: Timestamp.Create(DateTimeOffset.UtcNow.AddHours(-2)),
            presentTime: Timestamp.Create(DateTimeOffset.UtcNow.AddHours(-2).AddMinutes(5)),
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: 401
        );
        var phase2 = new Phase(
            gate: "GATE2/40",
            length: 20,
            maxRecovery: 20,
            rest: null,
            ruleset: CompetitionRuleset.FEI,
            isFinal: true,
            compulsoryThresholdSpan: null,
            startTime: null,
            arriveTime: null,
            presentTime: null,
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: 402
        );

        return new Participation(
            ParticipationCategory.Senior,
            competition,
            combination,
            new([phase1, phase2]),
            null,
            eventId,
            participationId
        );
    }

    sealed class EnduranceEventRepository : RecordingRepository<EnduranceEvent>, IEnduranceEventRepository
    {
        public EnduranceEventRepository(IEnumerable<EnduranceEvent> items)
            : base(items) { }

        public Task<IEnumerable<EnduranceEvent>> ReadActive()
        {
            return Task.FromResult<IEnumerable<EnduranceEvent>>([]);
        }

        public Task<IEnumerable<EnduranceEvent>> ReadPast()
        {
            return ReadMany();
        }

        public Task<EnduranceEvent> Start(int upcomingEventId)
        {
            throw new NotSupportedException();
        }

        public Task Reset()
        {
            throw new NotSupportedException();
        }
    }

    sealed class ParticipationPastRepository : RecordingRepository<Participation>, IPastParticipationRepository
    {
        public ParticipationPastRepository(IEnumerable<Participation> items)
            : base(items) { }

        public Task<IEnumerable<Participation>> ReadForEvent(int eventId)
        {
            return ReadMany();
        }
    }

    sealed class RankingPastRepository : RecordingRepository<Ranking>, IPastRankingRepository
    {
        public RankingPastRepository(IEnumerable<Ranking> items)
            : base(items) { }

        public Task<IEnumerable<Ranking>> ReadForEvent(int eventId)
        {
            return ReadMany();
        }
    }

    sealed class OfficialPastRepository : RecordingRepository<Official>, IPastOfficialRepository
    {
        public OfficialPastRepository(IEnumerable<Official> items)
            : base(items) { }

        public Task<IEnumerable<Official>> ReadForEvent(int eventId)
        {
            return ReadMany();
        }
    }

    abstract class RecordingRepository<T> : IRepository<T>
        where T : class, IEntity
    {
        readonly List<T> _items;

        protected RecordingRepository(IEnumerable<T>? items = null)
        {
            _items = items?.ToList() ?? [];
        }

        public Task Create(T item)
        {
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
