using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using Not.Krud.ServiceRegistration;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using NTS.Judge.Contracts.Features.Setup.Athletes;
using NTS.Judge.Contracts.Features.Setup.Clubs;
using NTS.Judge.Contracts.Features.Setup.Horses;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Combinations;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Loops;
using NTS.Judge.Features.Setup.Athletes;
using NTS.Judge.Features.Setup.Horses;
using NTS.Judge.Features.Setup.ConfigureEvents.Combinations;
using NTS.Judge.Features.Setup.ConfigureEvents.Loops;

namespace NTS.Judge.Tests.Krud;

public class KrudReflectionTests
{
    [Fact]
    public void RegisterAggregate_ConfigureEvent_RegistersMirrorsForNestedPrincipals()
    {
        using var scenario = ReflectionScenario.Create();

        Assert.Single(scenario.Provider.GetServices<IKrudMirrorService<Loop>>());
        Assert.Single(scenario.Provider.GetServices<IKrudMirrorService<Combination>>());
        Assert.Single(scenario.Provider.GetServices<IKrudMirrorService<Athlete>>());
        Assert.Single(scenario.Provider.GetServices<IKrudMirrorService<Horse>>());
    }

    [Fact]
    public void RegisterAggregate_Athlete_RegistersMirrorForClub()
    {
        using var scenario = ReflectionScenario.Create();

        Assert.Single(scenario.Provider.GetServices<IKrudMirrorService<Club>>());
    }

    [Fact]
    public async Task LoopServiceUpdate_ReflectsMatchingPhasesAndPersistsConfigureEvent()
    {
        using var scenario = ReflectionScenario.Create();
        scenario.ActivateEventContext();
        var service = scenario.CreateLoopService();

        await service.Update(new LoopFormModel { Id = scenario.LoopInUse.Id, Distance = 45 });

        var persisted = scenario.ConfigureEvents.Items.Single();
        Assert.Equal(45, persisted.Competitions.Single().Phases.First().Loop.Distance);
        Assert.True(scenario.ConfigureEvents.UpdateCalls > 0);
    }

    [Fact]
    public async Task CombinationServiceUpdate_ReflectsMatchingParticipations()
    {
        using var scenario = ReflectionScenario.Create();
        scenario.ActivateEventContext();
        var service = scenario.CreateCombinationService();

        await service.Update(
            new CombinationFormModel
            {
                Id = scenario.CombinationInUse.Id,
                Number = scenario.CombinationInUse.Number,
                Athlete = new Athlete(new Person(["Updated", "Again"]), null, scenario.Country, null, id: 5001),
                Horse = scenario.HorseInUse,
            }
        );

        Assert.Equal(
            "Updated Again",
            scenario.Event.Competitions.Single().Participations.First().Combination.Athlete.ToString()
        );
    }

    [Fact]
    public async Task AthleteServiceUpdate_ReflectsCombinationsThenParticipations()
    {
        using var scenario = ReflectionScenario.Create();
        scenario.ActivateEventContext();
        var service = scenario.CreateAthleteService();

        await service.Update(
            new AthleteFormModel
            {
                Id = scenario.AthleteInUse.Id,
                Names = "Updated Rider",
                Country = scenario.Country,
                Club = scenario.ClubInUse,
            }
        );

        Assert.Equal("Updated Rider", scenario.Event.Combinations.First().Athlete.ToString());
        Assert.Equal(
            "Updated Rider",
            scenario.Event.Competitions.Single().Participations.First().Combination.Athlete.ToString()
        );
    }

    [Fact]
    public async Task HorseServiceUpdate_ReflectsCombinationsThenParticipations()
    {
        using var scenario = ReflectionScenario.Create();
        scenario.ActivateEventContext();
        var service = scenario.CreateHorseService();

        await service.Update(
            new HorseFormModel
            {
                Id = scenario.HorseInUse.Id,
                Name = "Updated Horse",
                FeiId = null,
            }
        );

        Assert.Equal("Updated Horse", scenario.Event.Combinations.First().Horse.Name);
        Assert.Equal(
            "Updated Horse",
            scenario.Event.Competitions.Single().Participations.First().Combination.Horse.Name
        );
    }

    [Fact]
    public async Task ClubMirror_ReflectsAthletesAndDispatchesConfigureEventMirrors()
    {
        using var scenario = ReflectionScenario.Create();
        scenario.ActivateEventContext();
        var mirror = scenario.Provider.GetServices<IKrudMirrorService<Club>>().Single();

        await mirror.Reflect(new Club("Updated Club", scenario.ClubInUse.Id));

        Assert.Equal("Updated Club", scenario.Athletes.Items.Single(x => x.Id == scenario.AthleteInUse.Id).Club!.Name);
        Assert.Equal("Updated Club", scenario.Event.Combinations.First().Athlete.Club!.Name);
        Assert.Equal(
            "Updated Club",
            scenario.Event.Competitions.Single().Participations.First().Combination.Athlete.Club!.Name
        );
    }

    [Fact]
    public async Task GraphMirror_WhenNoActiveRootSet_DoesNotPersist()
    {
        using var scenario = ReflectionScenario.Create();
        var mirror = scenario.Provider.GetServices<IKrudMirrorService<Athlete>>().Single();

        await mirror.Reflect(
            new Athlete(new Person(["No", "Context"]), null, scenario.Country, null, id: scenario.AthleteInUse.Id)
        );

        Assert.Equal(0, scenario.ConfigureEvents.UpdateCalls);
        Assert.Equal("Athlete A", scenario.Event.Combinations.First().Athlete.ToString());
    }

    [Fact]
    public async Task GraphMirror_WhenNoMatchingReferencesExist_DoesNotPersist()
    {
        using var scenario = ReflectionScenario.Create();
        scenario.ActivateEventContext();
        var mirror = scenario.Provider.GetServices<IKrudMirrorService<Athlete>>().Single();

        await mirror.Reflect(
            new Athlete(new Person(["Unused", "Updated"]), null, scenario.Country, null, id: scenario.AthleteUnused.Id)
        );

        Assert.Equal(0, scenario.ConfigureEvents.UpdateCalls);
        Assert.Equal("Athlete A", scenario.Event.Combinations.First().Athlete.ToString());
    }

    sealed class ReflectionScenario : IDisposable
    {
        ReflectionScenario(
            ServiceProvider provider,
            RecordingRepository<ConfigureEvent> configureEvents,
            RecordingRepository<Athlete> athletes,
            Country country,
            Club clubInUse,
            Athlete athleteInUse,
            Athlete athleteUnused,
            Horse horseInUse,
            Combination combinationInUse,
            Loop loopInUse,
            ConfigureEvent @event
        )
        {
            Provider = provider;
            ConfigureEvents = configureEvents;
            Athletes = athletes;
            Country = country;
            ClubInUse = clubInUse;
            AthleteInUse = athleteInUse;
            AthleteUnused = athleteUnused;
            HorseInUse = horseInUse;
            CombinationInUse = combinationInUse;
            LoopInUse = loopInUse;
            Event = @event;
        }

        public ServiceProvider Provider { get; }
        public RecordingRepository<ConfigureEvent> ConfigureEvents { get; }
        public RecordingRepository<Athlete> Athletes { get; }
        public Country Country { get; }
        public Club ClubInUse { get; }
        public Athlete AthleteInUse { get; }
        public Athlete AthleteUnused { get; }
        public Horse HorseInUse { get; }
        public Combination CombinationInUse { get; }
        public Loop LoopInUse { get; }
        public ConfigureEvent Event { get; }

        public static ReflectionScenario Create()
        {
            var country = new Country(100, "Bulgaria", "BG", "BUL", "bg-BG");
            var clubInUse = new Club("Club A", id: 2001);
            var otherClub = new Club("Club B", id: 2002);

            var athleteInUse = new Athlete(new Person(["Athlete", "A"]), null, country, clubInUse, id: 5001);
            var athleteOther = new Athlete(new Person(["Athlete", "B"]), null, country, otherClub, id: 5002);
            var athleteUnused = new Athlete(new Person(["Athlete", "Unused"]), null, country, null, id: 5003);

            var horseInUse = new Horse("Horse A", null, id: 6001);
            var horseOther = new Horse("Horse B", null, id: 6002);
            var horseUnused = new Horse("Horse Unused", null, id: 6003);

            var combinationInUse = new Combination(number: 1, athlete: athleteInUse, horse: horseInUse, id: 7001);
            var combinationOther = new Combination(number: 2, athlete: athleteOther, horse: horseOther, id: 7002);

            var loopInUse = new Loop(distance: 20, id: 8001);
            var loopOther = new Loop(distance: 30, id: 8002);

            var competition = new Competition(
                name: "Competition 1",
                type: CompetitionType.Qualification,
                ruleset: CompetitionRuleset.Regional,
                start: DateTimeOffset.UtcNow.AddDays(1),
                compulsoryThresholdSpan: TimeSpan.FromMinutes(10),
                feiId: null,
                feiRule: null,
                feiScheduleNumber: null,
                phases:
                [
                    new Phase(loopInUse, recovery: 10, rest: 40, id: 9001),
                    new Phase(loopOther, recovery: 10, rest: 40, id: 9002),
                ],
                participations:
                [
                    new Participation(
                        false,
                        combinationInUse,
                        ParticipationCategory.Senior,
                        null,
                        null,
                        null,
                        id: 9101
                    ),
                    new Participation(
                        false,
                        combinationOther,
                        ParticipationCategory.Senior,
                        null,
                        null,
                        null,
                        id: 9102
                    ),
                ],
                id: 9201
            );

            var @event = new ConfigureEvent(
                name: "Event",
                location: "Sofia",
                country,
                showFeiId: null,
                feiId: null,
                feiEventCode: null,
                competitions: [competition],
                officials: [],
                loops: [loopInUse, loopOther],
                combinations: [combinationInUse, combinationOther],
                id: 10001
            );

            var configureEvents = new RecordingRepository<ConfigureEvent>([@event]);
            var athletes = new RecordingRepository<Athlete>([athleteInUse, athleteOther, athleteUnused]);
            var clubs = new RecordingRepository<Club>([clubInUse, otherClub]);
            var horses = new RecordingRepository<Horse>([horseInUse, horseOther, horseUnused]);

            var services = new ServiceCollection();
            services.AddSingleton<IRepository<ConfigureEvent>>(configureEvents);
            services.AddSingleton<IRepository<Athlete>>(athletes);
            services.AddSingleton<IRepository<Club>>(clubs);
            services.AddSingleton<IRepository<Horse>>(horses);
            services.ConfigureKrud().RegisterAggregate<ConfigureEvent>().RegisterAggregate<Athlete>();

            var provider = services.BuildServiceProvider();
            return new ReflectionScenario(
                provider,
                configureEvents,
                athletes,
                country,
                clubInUse,
                athleteInUse,
                athleteUnused,
                horseInUse,
                combinationInUse,
                loopInUse,
                @event
            );
        }

        public void ActivateEventContext()
        {
            foreach (var setter in Provider.GetRequiredService<IEnumerable<IKrudNodeSetter>>())
            {
                setter.SetParent(Event);
            }
        }

        public LoopService CreateLoopService()
        {
            return new LoopService(
                Provider.GetServices<IKrudMirrorService<Loop>>(),
                Provider.GetRequiredService<IRepository<Loop>>()
            );
        }

        public CombinationService CreateCombinationService()
        {
            return new CombinationService(
                Provider.GetRequiredService<IRepository<Combination>>(),
                Provider.GetServices<IKrudMirrorService<Combination>>()
            );
        }

        public AthleteService CreateAthleteService()
        {
            return new AthleteService(
                Provider.GetRequiredService<IRepository<Athlete>>(),
                Provider.GetServices<IKrudMirrorService<Athlete>>()
            );
        }

        public HorseService CreateHorseService()
        {
            return new HorseService(
                Provider.GetRequiredService<IRepository<Horse>>(),
                Provider.GetServices<IKrudMirrorService<Horse>>()
            );
        }

        public void Dispose()
        {
            Provider.Dispose();
        }
    }

    sealed class RecordingRepository<T> : IRepository<T>
        where T : class, IEntity
    {
        readonly List<T> _items;

        public RecordingRepository(IEnumerable<T>? items = null)
        {
            _items = items?.ToList() ?? [];
        }

        public int UpdateCalls { get; private set; }
        public IReadOnlyList<T> Items => _items.AsReadOnly();

        public Task Create(T item)
        {
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
            UpdateCalls++;
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
