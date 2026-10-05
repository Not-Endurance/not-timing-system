using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain;
using NTS.Application.Core;
using NTS.Application.HistoricEvents;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The Historic Event pages show the Results of a Ranking composed from the Participations the service read once for the
/// Event and from the ids the Ranking holds (#624, ADR-0006).
/// </summary>
public sealed class HistoricEventServiceTests
{
    static readonly Guid EVENT_ID = TestId.Of(1);

    [Fact]
    public async Task The_document_of_a_Ranking_is_composed_from_the_Participations_it_names()
    {
        var arrive = DateTimeOffset.Now.AddHours(-3);
        var first = ParticipationFixtures.CompletedAt(1, arrive);
        var second = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(30));
        var ranking = CreateRanking([new RankingEntry(second.Id, false), new RankingEntry(first.Id, false)]);
        var service = CreateService([first, second], [ranking]);

        await service.LoadEvent(EVENT_ID);

        var document = Assert.IsType<ResultsDocument>(service.Document);
        Assert.Equal([first.Id, second.Id], document.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], document.Entries.Select(x => x.Rank));
        Assert.Same(first, document.Entries[0].Participation);
    }

    [Fact]
    public async Task A_Participation_in_two_Rankings_is_marked_in_each_by_that_Ranking_alone()
    {
        var arrive = DateTimeOffset.Now.AddHours(-3);
        var first = ParticipationFixtures.CompletedAt(1, arrive);
        var second = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(30));
        var regular = CreateRanking([new RankingEntry(first.Id, false), new RankingEntry(second.Id, false)]);
        var custom = CreateRanking([new RankingEntry(first.Id, true), new RankingEntry(second.Id, false)]);
        var service = CreateService([first, second], [regular, custom]);
        await service.LoadEvent(EVENT_ID);

        var inTheRegularOne = service.Document!.Entries.Select(x => x.ParticipationId).ToList();
        service.Select(custom);
        var inTheCustomOne = service.Document!.Entries.Select(x => x.ParticipationId).ToList();

        Assert.Equal([first.Id, second.Id], inTheRegularOne);
        Assert.Equal([second.Id, first.Id], inTheCustomOne);
    }

    static Ranking CreateRanking(IEnumerable<RankingEntry> entries)
    {
        return new Ranking(
            "CEI 1*",
            CompetitionRuleset.Regional,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            EVENT_ID
        );
    }

    static HistoricEventService CreateService(IEnumerable<Participation> participations, IEnumerable<Ranking> rankings)
    {
        var historicEvent = new EventInformation(
            new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG"),
            "Event",
            "Location",
            new EventSpan(DateTimeOffset.Now.Date.AddDays(-1), DateTimeOffset.Now.Date),
            null,
            id: EVENT_ID
        );

        return new HistoricEventService(
            new HistoricEvents(historicEvent),
            new ReadOnlyRepository<Participation>(participations),
            new ReadOnlyRepository<Ranking>(rankings),
            new ReadOnlyRepository<Official>([])
        );
    }

    class ReadOnlyRepository<T> : IRepository<T>
        where T : Entity
    {
        readonly List<T> _items;

        public ReadOnlyRepository(IEnumerable<T> items)
        {
            _items = [.. items];
        }

        public Task<T?> Read(Guid id)
        {
            return Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        }

        public Task<T?> Read(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult(_items.AsQueryable().FirstOrDefault(filter));
        }

        public Task<IEnumerable<T>> ReadMany()
        {
            return Task.FromResult<IEnumerable<T>>(_items.ToArray());
        }

        public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult<IEnumerable<T>>(_items.AsQueryable().Where(filter).ToArray());
        }

        public Task Create(T item)
        {
            throw new NotSupportedException("The service only reads.");
        }

        public Task Update(T item)
        {
            throw new NotSupportedException("The service only reads.");
        }

        public Task Delete(T item)
        {
            throw new NotSupportedException("The service only reads.");
        }

        public Task DeleteMany(IEnumerable<T> items)
        {
            throw new NotSupportedException("The service only reads.");
        }

        public Task DeleteMany(Expression<Func<T, bool>> filter)
        {
            throw new NotSupportedException("The service only reads.");
        }
    }

    sealed class HistoricEvents : ReadOnlyRepository<EventInformation>, IEventInformationRepository
    {
        readonly EventInformation _historicEvent;

        public HistoricEvents(EventInformation historicEvent)
            : base([historicEvent])
        {
            _historicEvent = historicEvent;
        }

        public Task<IEnumerable<EventInformation>> ReadHistoric()
        {
            return Task.FromResult<IEnumerable<EventInformation>>([_historicEvent]);
        }

        public Task<IEnumerable<EventInformation>> ReadLive()
        {
            throw new NotSupportedException("The service reads Historic Events only.");
        }

        public Task<EventInformation> Start(Guid configureEventId)
        {
            throw new NotSupportedException("The service reads Historic Events only.");
        }

        public Task Reset()
        {
            throw new NotSupportedException("The service reads Historic Events only.");
        }
    }
}
