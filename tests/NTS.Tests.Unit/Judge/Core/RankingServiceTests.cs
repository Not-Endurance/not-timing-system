using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Domain.Abstractions;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Judge.Contracts.Features.Core.Rankings;
using NTS.Judge.Features.Core.Rankings;

namespace NTS.Judge.Tests.Core;

public class RankingServiceTests
{
    [Fact]
    public async Task Handle_WhenPhaseCompletedArrivesBeforeInitialization_LoadsAndUpdatesRankings()
    {
        var originalParticipation = CreateParticipation(
            number: 42,
            eventId: 14,
            participationId: 301,
            combinationId: 201
        );
        var updatedParticipation = CreateParticipation(
            number: 42,
            eventId: 14,
            participationId: 301,
            combinationId: 201
        );
        var ranking = new Ranking(
            "General Classification",
            CompetitionRuleset.FEI,
            CompetitionType.Qualification,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            [new RankingEntry(originalParticipation, 1, false, 701)],
            14,
            601
        );
        var rankings = new RecordingRepository<Ranking>([ranking]);
        var service = new RankingService(
            new TestSocketContext { Event = CreateEvent(14) },
            rankings,
            new RecordingRepository<Official>()
        );

        await service.Handle(new PhaseCompleted(updatedParticipation), CancellationToken.None);

        var inMemoryRanking = Assert.Single(service.Rankings);
        var storedRanking = Assert.Single(rankings.Items);

        Assert.Same(updatedParticipation, Assert.Single(inMemoryRanking.Entries).Participation);
        Assert.Same(updatedParticipation, Assert.Single(storedRanking.Entries).Participation);
        Assert.Equal(storedRanking.Id, service.Current.Id);
    }

    static EnduranceEvent CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            country,
            "Sofia",
            "Sofia",
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
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
        var phase = new Phase(
            gate: "GATE1/40",
            length: 40,
            maxRecovery: 20,
            rest: null,
            ruleset: CompetitionRuleset.FEI,
            isFinal: true,
            compulsoryThresholdSpan: null,
            startTime: Timestamp.Create(new DateTimeOffset(2026, 3, 22, 8, 0, 0, TimeSpan.Zero)),
            arriveTime: null,
            presentTime: null,
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: 401
        );

        return new Participation(
            ParticipationCategory.Senior,
            competition,
            combination,
            new PhaseCollection([phase]),
            null,
            eventId,
            participationId
        );
    }

    sealed class TestSocketContext : INtsSocketContext
    {
        public bool IsConnected => Event != null;
        public SocketConnectionStatus Status =>
            IsConnected ? SocketConnectionStatus.Connected : SocketConnectionStatus.Disconnected;
        public EnduranceEvent? Event { get; set; }
    }

    sealed class RecordingRepository<T> : IRepository<T>
        where T : class, IEntity
    {
        readonly List<T> _items;

        public RecordingRepository(IEnumerable<T>? items = null)
        {
            _items = items?.ToList() ?? [];
        }

        public IReadOnlyList<T> Items => _items.AsReadOnly();

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
