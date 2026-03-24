using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.RPC.SignalR;
using Not.Domain.Abstractions;
using NTS.Application.Socket;
using NTS.Application.Core;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Judge.Features.Core.Handouts;

namespace NTS.Judge.Tests.Core;

public class HandoutsServiceTests
{
    [Fact]
    public async Task Handle_WhenPhaseCompletedIsRaisedAgainForSameParticipation_KeepsSingleHandout()
    {
        var participation = CreateParticipation(number: 42, eventId: 14, participationId: 301, combinationId: 201);
        var handouts = new RecordingRepository<Handout>();
        var service = new HandoutsService(
            new TestSocketContext { Event = CreateEvent(14) },
            handouts,
            new RecordingRepository<Participation>([participation]),
            new RecordingRepository<Official>()
        );
        var notification = new PhaseCompleted(participation);

        await service.Handle(notification, CancellationToken.None);
        var firstDocument = Assert.Single(service.Documents);

        await service.Handle(notification, CancellationToken.None);

        var document = Assert.Single(service.Documents);
        Assert.Equal(firstDocument.Id, document.Id);
        Assert.Equal(document.Id, Assert.Single(handouts.Items).Id);
    }

    [Fact]
    public async Task Handle_WhenExistingHandoutsShareParticipation_ReplacesThemByParticipationId()
    {
        var participation = CreateParticipation(number: 42, eventId: 14, participationId: 301, combinationId: 201);
        var preservedHandout = new Handout(participation, 901);
        var duplicateHandout = new Handout(participation, 902);
        var handouts = new RecordingRepository<Handout>([preservedHandout, duplicateHandout]);
        var service = new HandoutsService(
            new TestSocketContext { Event = CreateEvent(14) },
            handouts,
            new RecordingRepository<Participation>([participation]),
            new RecordingRepository<Official>()
        );

        await service.Handle(new PhaseCompleted(participation), CancellationToken.None);

        var stored = Assert.Single(handouts.Items);
        var document = Assert.Single(service.Documents);

        Assert.Equal(preservedHandout.Id, stored.Id);
        Assert.Equal(participation.Id, stored.Participation.Id);
        Assert.Equal(stored.Id, document.Id);
        Assert.Equal(participation.Id, document.ParticipationId);
    }

    [Fact]
    public void HandoutModel_MapToEntity_PreservesHandoutIdentity()
    {
        var participation = CreateParticipation(number: 42, eventId: 14, participationId: 301, combinationId: 201);
        var model = HandoutModel.From(new Handout(participation, 901));

        var handout = model.MapToEntity();

        Assert.Equal(901, handout.Id);
        Assert.Equal(participation.Id, handout.Participation.Id);
    }

    static EnduranceEvent CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            new PopulatedPlace(country, "Sofia", "Sofia"),
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
