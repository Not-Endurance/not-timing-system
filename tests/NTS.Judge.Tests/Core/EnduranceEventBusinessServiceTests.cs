using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Exceptions;
using CoreEnduranceEventModel = NTS.Application.Core.EnduranceEventModel;
using CoreOfficialModel = NTS.Application.Core.OfficialModel;
using CoreParticipationModel = NTS.Application.Core.ParticipationModel;
using CoreRankingModel = NTS.Application.Core.RankingModel;
using SetupUpcomingEventModel = NTS.Application.Setup.UpcomingEventModel;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;
using NTS.Nexus.HTTP.Functions.Event;
using SetupOfficial = NTS.Domain.Setup.Aggregates.UpcomingEvents.Official;
using SetupParticipation = NTS.Domain.Setup.Aggregates.UpcomingEvents.Participation;

namespace NTS.Judge.Tests.Core;

public class EnduranceEventBusinessServiceTests
{
    [Fact]
    public async Task Start_WhenUpcomingEventIdTargetsRequestedEvent_CreatesCoreEventAndEntriesWithSameId()
    {
        var events = new RecordingRepository<CoreEnduranceEventModel>();
        var officials = new RecordingRepository<CoreOfficialModel>();
        var participations = new RecordingRepository<CoreParticipationModel>();
        var rankings = new RecordingRepository<CoreRankingModel>();
        var service = CreateService(
            [CreateValidEventModel(1), CreateValidEventModel(2)],
            events,
            officials,
            participations,
            rankings
        );

        var result = await service.Start(2);

        Assert.Equal(2, result.Id);
        Assert.Equal(2, Assert.Single(events.CreatedItems).Id);
        Assert.Single(officials.CreatedItems);
        Assert.Single(participations.CreatedItems);
        Assert.Single(rankings.CreatedItems);
    }

    [Fact]
    public async Task Start_GeneratesNewCoreOfficialAndParticipationIds()
    {
        var officials = new RecordingRepository<CoreOfficialModel>();
        var participations = new RecordingRepository<CoreParticipationModel>();
        var service = CreateService(
            [CreateValidEventModel(2)],
            officials: officials,
            participations: participations,
            rankings: new RecordingRepository<CoreRankingModel>()
        );

        await service.Start(2);

        Assert.NotEmpty(officials.CreatedItems);
        Assert.NotEmpty(participations.CreatedItems);
        Assert.DoesNotContain(officials.CreatedItems, x => x.Id == 21);
        Assert.DoesNotContain(participations.CreatedItems, x => x.Id == 21);
    }

    [Fact]
    public async Task Start_WhenUpcomingEventIdMissing_ThrowsGuardException()
    {
        var service = CreateService([CreateValidEventModel(1)]);

        var exception = await Assert.ThrowsAsync<GuardException>(() => service.Start(42));

        Assert.Contains("42", exception.Message);
    }

    static EnduranceEventBusinessService CreateService(
        IEnumerable<SetupUpcomingEventModel> upcomingEvents,
        RecordingRepository<CoreEnduranceEventModel>? events = null,
        RecordingRepository<CoreOfficialModel>? officials = null,
        RecordingRepository<CoreParticipationModel>? participations = null,
        RecordingRepository<CoreRankingModel>? rankings = null
    )
    {
        return new EnduranceEventBusinessService(
            new RecordingRepository<SetupUpcomingEventModel>(upcomingEvents),
            events ?? new RecordingRepository<CoreEnduranceEventModel>(),
            officials ?? new RecordingRepository<CoreOfficialModel>(),
            participations ?? new RecordingRepository<CoreParticipationModel>(),
            rankings ?? new RecordingRepository<CoreRankingModel>()
        );
    }

    static SetupUpcomingEventModel CreateValidEventModel(int id, int? competitionId = null, int? participationNumber = null)
    {
        return SetupUpcomingEventModel.From(CreateValidEvent(id, competitionId, participationNumber));
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
        var official = new SetupOfficial(new Person(["Judge", $"{id}"]), OfficialRole.GroundJuryPresident, id * 10 + 1);

        return new UpcomingEvent(
            $"Event {id}",
            "Sofia",
            country,
            null,
            null,
            null,
            [competition],
            [official],
            [loop],
            [combination],
            id
        );
    }

    sealed class RecordingRepository<T> : IRepository<T>
        where T : class
    {
        readonly List<T> _items;

        public RecordingRepository(IEnumerable<T>? items = null)
        {
            _items = items?.ToList() ?? [];
        }

        public List<T> CreatedItems { get; } = [];

        public Task Create(T item)
        {
            CreatedItems.Add(item);
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
            var entity = _items.OfType<dynamic>().FirstOrDefault(x => x.Id == id);
            return Task.FromResult((T?)entity);
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
            return Task.CompletedTask;
        }

        public Task Delete(T item)
        {
            return Task.CompletedTask;
        }

        public Task Delete(int id)
        {
            return Task.CompletedTask;
        }

        public Task Delete(Expression<Func<T, bool>> filter)
        {
            return Task.CompletedTask;
        }

        public Task Delete(IEnumerable<T> items)
        {
            return Task.CompletedTask;
        }
    }
}
