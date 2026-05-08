using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using NTS.Nexus.HTTP.Functions.Event;
using CoreEventInformationModel = NTS.Application.Contracts.Core.Models.EventInformationModel;
using CoreOfficialModel = NTS.Application.Contracts.Core.Models.OfficialModel;
using CoreParticipationModel = NTS.Application.Contracts.Core.Models.ParticipationModel;
using CoreRankingModel = NTS.Application.Contracts.Core.Models.RankingModel;
using SetupOfficial = NTS.Domain.Setup.Aggregates.ConfigureEvents.Official;
using SetupParticipation = NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation;
using SetupConfigureEventModel = NTS.Application.Contracts.Setup.Models.ConfigureEventModel;

namespace NTS.Judge.Tests.Core;

public class EventInformationBusinessServiceTests
{
    [Fact]
    public async Task Start_WhenConfigureEventIdTargetsRequestedEvent_CreatesCoreEventAndEntriesWithSameId()
    {
        var events = new RecordingRepository<CoreEventInformationModel>();
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
    public async Task Start_WhenSetupOfficialHasLinkedUser_PreservesUserIdOnCreatedCoreOfficial()
    {
        var officials = new RecordingRepository<CoreOfficialModel>();
        var service = CreateService([CreateValidEventModel(2, officialUserId: 222)], officials: officials);

        await service.Start(2);

        var createdOfficial = Assert.Single(officials.CreatedItems);
        Assert.Equal(222, createdOfficial.UserId);
    }

    [Fact]
    public void CoreOfficialModel_RoundTrip_PreservesUserId()
    {
        var official = new NTS.Domain.Core.Aggregates.Official(
            new Person(["Judge", "User"]),
            OfficialRole.GroundJuryPresident,
            eventId: 42,
            id: 7,
            userId: 99
        );

        var model = CoreOfficialModel.MapFrom(official);
        var restored = model.MapToEntity();

        Assert.Equal(99, model.UserId);
        Assert.Equal(99, restored.UserId);
    }

    [Fact]
    public async Task Start_WhenParticipationHasFutureStartTimeOverride_CreatesParticipationWithOverrideStartTime()
    {
        var overrideStart = DateTimeOffset.Now.AddHours(2);
        var participations = new RecordingRepository<CoreParticipationModel>();
        var service = CreateService(
            [
                CreateValidEventModel(
                    2,
                    competitionStart: DateTimeOffset.Now.AddHours(-2),
                    startTimeOverride: overrideStart
                ),
            ],
            participations: participations,
            rankings: new RecordingRepository<CoreRankingModel>()
        );

        await service.Start(2);

        var createdParticipation = Assert.Single(participations.CreatedItems);
        Assert.Equal(overrideStart.ToUniversalTime(), createdParticipation.Phases[0].StartTime);
    }

    [Fact]
    public async Task Start_WhenConfigureEventIdMissing_ThrowsGuardException()
    {
        var service = CreateService([CreateValidEventModel(1)]);

        var exception = await Assert.ThrowsAsync<GuardException>(() => service.Start(42));

        Assert.Contains("42", exception.Message);
    }

    [Fact]
    public async Task Start_WhenCompetitionHasNoParticipations_ThrowsDomainExceptionBeforeCreatingCoreEntries()
    {
        var events = new RecordingRepository<CoreEventInformationModel>();
        var service = CreateService(
            [SetupConfigureEventModel.From(CreateInvalidEventWithoutParticipations(2))],
            events: events
        );

        var exception = await Assert.ThrowsAsync<DomainException>(() => service.Start(2));

        Assert.Contains("must have at least one participation", exception.Message);
        Assert.Empty(events.CreatedItems);
    }

    static EventInformationBusinessService CreateService(
        IEnumerable<SetupConfigureEventModel> configureEvents,
        RecordingRepository<CoreEventInformationModel>? events = null,
        RecordingRepository<CoreOfficialModel>? officials = null,
        RecordingRepository<CoreParticipationModel>? participations = null,
        RecordingRepository<CoreRankingModel>? rankings = null
    )
    {
        return new EventInformationBusinessService(
            new RecordingRepository<SetupConfigureEventModel>(configureEvents),
            events ?? new RecordingRepository<CoreEventInformationModel>(),
            officials ?? new RecordingRepository<CoreOfficialModel>(),
            participations ?? new RecordingRepository<CoreParticipationModel>(),
            rankings ?? new RecordingRepository<CoreRankingModel>()
        );
    }

    static SetupConfigureEventModel CreateValidEventModel(
        int id,
        int? competitionId = null,
        int? participationNumber = null,
        DateTimeOffset? competitionStart = null,
        DateTimeOffset? startTimeOverride = null,
        int? officialUserId = null
    )
    {
        return SetupConfigureEventModel.From(
            CreateValidEvent(
                id,
                competitionId,
                participationNumber,
                competitionStart,
                startTimeOverride,
                officialUserId
            )
        );
    }

    static ConfigureEvent CreateValidEvent(
        int id,
        int? competitionId = null,
        int? participationNumber = null,
        DateTimeOffset? competitionStart = null,
        DateTimeOffset? startTimeOverride = null,
        int? officialUserId = null
    )
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
            startTimeOverride: startTimeOverride,
            maxSpeedOverride: null,
            minSpeedOverride: null,
            id: id * 10 + 1
        );
        var competition = new Competition(
            $"Competition {id}",
            CompetitionType.Qualification,
            CompetitionRuleset.FEI,
            competitionStart ?? DateTimeOffset.UtcNow.AddHours(id),
            null,
            null,
            null,
            null,
            [phase],
            [participation],
            competitionId ?? id * 10 + 1
        );
        var officialUser =
            officialUserId == null ? null : new User($"judge{id}@example.com", $"Judge {id}", id: officialUserId.Value);
        var official = new SetupOfficial(
            new Person(["Judge", $"{id}"]),
            OfficialRole.GroundJuryPresident,
            id * 10 + 1,
            officialUser
        );

        return new ConfigureEvent(
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

    static ConfigureEvent CreateInvalidEventWithoutParticipations(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["John", "Doe"]), null, country, null, id * 10 + 1);
        var horse = new Horse($"Horse {id}", null, id * 10 + 1);
        var combination = new Combination(id * 100 + 1, athlete, horse, id * 10 + 1);
        var loop = new Loop(40, id * 10 + 1);
        var phase = new Phase(loop, 40, null, id * 10 + 1);
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
            [],
            id * 10 + 1
        );

        return new ConfigureEvent(
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

        public Task DeleteMany(Expression<Func<T, bool>> filter)
        {
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<T> items)
        {
            return Task.CompletedTask;
        }
    }
}
