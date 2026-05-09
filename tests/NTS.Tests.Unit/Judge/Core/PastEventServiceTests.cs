using System.Linq.Expressions;
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
        var eventInformation = CreateEvent(14);
        var participation = CreateParticipation(number: 42, eventId: 14, participationId: 301, combinationId: 201);
        var otherParticipation = CreateParticipation(number: 43, eventId: 99, participationId: 302, combinationId: 202);
        var firstRanking = CreateRanking("General Classification", participation, 601);
        var secondRanking = CreateRanking("Best Condition", participation, 602);
        var otherRanking = CreateRanking("Other Event", otherParticipation, 603);
        var official = new Official(
            new Person(["Jane", "Doe"]),
            OfficialRole.GroundJuryPresident,
            eventId: 14,
            id: 701
        );
        var otherOfficial = new Official(new Person(["Other"]), OfficialRole.Steward, eventId: 99, id: 702);
        var service = new PastEventService(
            new EventInformationRepository([eventInformation]),
            new RecordingRepository<Participation>([participation, otherParticipation]),
            new RecordingRepository<Ranking>([firstRanking, secondRanking, otherRanking]),
            new RecordingRepository<Official>([official, otherOfficial])
        );

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

    static EventInformation CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EventInformation(
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

    sealed class EventInformationRepository : RecordingRepository<EventInformation>, IEventInformationRepository
    {
        public EventInformationRepository(IEnumerable<EventInformation> items)
            : base(items) { }

        public Task<IEnumerable<EventInformation>> ReadActive()
        {
            return Task.FromResult<IEnumerable<EventInformation>>([]);
        }

        public Task<IEnumerable<EventInformation>> ReadPast()
        {
            return ReadMany();
        }

        public Task<EventInformation> Start(int configureEventId)
        {
            throw new NotSupportedException();
        }

        public Task Reset()
        {
            throw new NotSupportedException();
        }

        public Task Deactivate()
        {
            throw new NotSupportedException();
        }
    }

    class RecordingRepository<T> : IRepository<T>
        where T : class, IEntity
    {
        readonly List<T> _items;

        public RecordingRepository(IEnumerable<T>? items = null)
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

        public Task DeleteMany(Expression<Func<T, bool>> filter)
        {
            var predicate = filter.Compile();
            _items.RemoveAll(x => predicate(x));
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<T> items)
        {
            var ids = items.Select(x => x.Id).ToHashSet();
            _items.RemoveAll(x => ids.Contains(x.Id));
            return Task.CompletedTask;
        }
    }
}
