using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Domain;
using Not.Domain.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.ServiceRegistration;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Combinations;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Loops;
using NTS.Judge.Features.Setup.ConfigureEvents.Combinations;
using NTS.Judge.Features.Setup.ConfigureEvents.Loops;

namespace NTS.Judge.Tests.Krud;

public class KrudCascadingDeleteTests
{
    [Fact]
    public async Task CascadeDelete_DependencyDiscovery_MapsCombinationToParticipation()
    {
        using var scenario = CreateScenario();

        var impact = await scenario.CombinationCascade.PreviewDelete(scenario.CombinationInUse);

        Assert.Contains(impact.Usages, x => x.Relation == "Participation.Combination");
        Assert.Equal(2, impact.Usages.Count(x => x.Relation == "Participation.Combination"));
    }

    [Fact]
    public async Task CascadeDelete_DependencyDiscovery_MapsLoopToPhase()
    {
        using var scenario = CreateScenario();

        var impact = await scenario.LoopCascade.PreviewDelete(scenario.LoopInUse);

        Assert.Contains(impact.Usages, x => x.Relation == "Phase.Loop");
        Assert.Equal(2, impact.Usages.Count(x => x.Relation == "Phase.Loop"));
    }

    [Fact]
    public async Task CascadeDelete_Combination_WithDependencies_RequiresConfirmation()
    {
        using var scenario = CreateScenario();

        await Assert.ThrowsAsync<DomainException>(() => scenario.CombinationService.Delete(scenario.CombinationInUse));

        Assert.Contains(scenario.Event.Combinations, x => x == scenario.CombinationInUse);
        Assert.Equal(3, scenario.Event.Competitions.SelectMany(x => x.Participations).Count());
    }

    [Fact]
    public async Task CascadeDelete_Loop_WithDependencies_RequiresConfirmation()
    {
        using var scenario = CreateScenario();

        await Assert.ThrowsAsync<DomainException>(() => scenario.LoopService.Delete(scenario.LoopInUse));

        Assert.Contains(scenario.Event.Loops, x => x == scenario.LoopInUse);
        Assert.Equal(4, scenario.Event.Competitions.SelectMany(x => x.Phases).Count());
    }

    [Fact]
    public async Task CascadeDelete_WithoutDependencies_DeletesDirectly()
    {
        using var scenario = CreateScenario();

        await scenario.CombinationService.Delete(scenario.UnusedCombination);

        Assert.DoesNotContain(scenario.Event.Combinations, x => x == scenario.UnusedCombination);
    }

    [Fact]
    public async Task CascadeDelete_Combination_Cascade_RemovesParticipationsAcrossCompetitions()
    {
        using var scenario = CreateScenario();

        await scenario.CombinationCascade.DeleteCascade(scenario.CombinationInUse);

        Assert.DoesNotContain(scenario.Event.Combinations, x => x == scenario.CombinationInUse);
        Assert.Equal(2, scenario.Event.Competitions.Count);
        Assert.All(
            scenario.Event.Competitions,
            competition =>
                Assert.DoesNotContain(
                    competition.Participations,
                    participation => participation.Combination == scenario.CombinationInUse
                )
        );
        Assert.Single(scenario.Event.Competitions.SelectMany(x => x.Participations));
    }

    [Fact]
    public async Task CascadeDelete_Loop_Cascade_RemovesPhasesAcrossCompetitions()
    {
        using var scenario = CreateScenario();

        await scenario.LoopCascade.DeleteCascade(scenario.LoopInUse);

        Assert.DoesNotContain(scenario.Event.Loops, x => x == scenario.LoopInUse);
        Assert.Equal(2, scenario.Event.Competitions.Count);
        Assert.All(
            scenario.Event.Competitions,
            competition => Assert.DoesNotContain(competition.Phases, phase => phase.Loop == scenario.LoopInUse)
        );
        Assert.Equal(2, scenario.Event.Competitions.SelectMany(x => x.Phases).Count());
    }

    static Scenario CreateScenario()
    {
        var country = new Country(id: 100, name: "Bulgaria", isoCode: "BG", nfCode: "BUL", locale: "bg-BG");

        var athleteA = new Athlete(new Person(["Athlete", "A"]), feiId: null, country, club: null, id: 5001);
        var athleteB = new Athlete(new Person(["Athlete", "B"]), feiId: null, country, club: null, id: 5002);
        var athleteC = new Athlete(new Person(["Athlete", "C"]), feiId: null, country, club: null, id: 5003);
        var horseA = new Horse("Horse A", feiId: null, id: 6001);
        var horseB = new Horse("Horse B", feiId: null, id: 6002);
        var horseC = new Horse("Horse C", feiId: null, id: 6003);

        var combinationInUse = new Combination(number: 1, athlete: athleteA, horse: horseA, id: 7001);
        var combinationOther = new Combination(number: 2, athlete: athleteB, horse: horseB, id: 7002);
        var unusedCombination = new Combination(number: 3, athlete: athleteC, horse: horseC, id: 7003);

        var loopInUse = new Loop(distance: 20, id: 8001);
        var loopOther = new Loop(distance: 30, id: 8002);

        var competitionOne = new Competition(
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
                new Participation(false, combinationInUse, ParticipationCategory.Senior, null, null, null, id: 9101),
                new Participation(false, combinationOther, ParticipationCategory.Senior, null, null, null, id: 9102),
            ],
            id: 9201
        );

        var competitionTwo = new Competition(
            name: "Competition 2",
            type: CompetitionType.Qualification,
            ruleset: CompetitionRuleset.Regional,
            start: DateTimeOffset.UtcNow.AddDays(2),
            compulsoryThresholdSpan: TimeSpan.FromMinutes(10),
            feiId: null,
            feiRule: null,
            feiScheduleNumber: null,
            phases:
            [
                new Phase(loopInUse, recovery: 10, rest: 40, id: 9003),
                new Phase(loopOther, recovery: 10, rest: 40, id: 9004),
            ],
            participations:
            [
                new Participation(false, combinationInUse, ParticipationCategory.Senior, null, null, null, id: 9103),
            ],
            id: 9202
        );

        var @event = new ConfigureEvent(
            name: "Event",
            location: "Sofia",
            country,
            showFeiId: null,
            feiId: null,
            feiEventCode: null,
            competitions: [competitionOne, competitionTwo],
            officials: [],
            loops: [loopInUse, loopOther],
            combinations: [combinationInUse, combinationOther, unusedCombination],
            id: 10001
        );

        var services = new ServiceCollection();
        services.AddSingleton<IRepository<ConfigureEvent>>(new ConfigureEventRepository(@event));
        services.ConfigureKrud().RegisterAggregate<ConfigureEvent>();
        var provider = services.BuildServiceProvider();

        SetNodeParents(provider, @event);

        var combinationRepository = provider.GetRequiredService<IRepository<Combination>>();
        var loopRepository = provider.GetRequiredService<IRepository<Loop>>();

        var combinationService = new CombinationService(combinationRepository, []);
        var loopService = new LoopService([], loopRepository);

        return new Scenario(
            provider,
            @event,
            combinationInUse,
            unusedCombination,
            loopInUse,
            combinationService,
            (IKrudListBehind<Combination>)combinationService,
            loopService,
            (IKrudListBehind<Loop>)loopService
        );
    }

    static void SetNodeParents(IServiceProvider provider, object value)
    {
        foreach (var setter in provider.GetRequiredService<IEnumerable<IKrudNodeSetter>>())
        {
            setter.SetParent(value);
        }
    }

    sealed class Scenario : IDisposable
    {
        public Scenario(
            ServiceProvider provider,
            ConfigureEvent @event,
            Combination combinationInUse,
            Combination unusedCombination,
            Loop loopInUse,
            CombinationService combinationService,
            IKrudListBehind<Combination> combinationCascade,
            LoopService loopService,
            IKrudListBehind<Loop> loopCascade
        )
        {
            Provider = provider;
            Event = @event;
            CombinationInUse = combinationInUse;
            UnusedCombination = unusedCombination;
            LoopInUse = loopInUse;
            CombinationService = combinationService;
            CombinationCascade = combinationCascade;
            LoopService = loopService;
            LoopCascade = loopCascade;
        }

        public ServiceProvider Provider { get; }
        public ConfigureEvent Event { get; }
        public Combination CombinationInUse { get; }
        public Combination UnusedCombination { get; }
        public Loop LoopInUse { get; }
        public CombinationService CombinationService { get; }
        public IKrudListBehind<Combination> CombinationCascade { get; }
        public LoopService LoopService { get; }
        public IKrudListBehind<Loop> LoopCascade { get; }

        public void Dispose()
        {
            Provider.Dispose();
        }
    }

    sealed class ConfigureEventRepository : IRepository<ConfigureEvent>
    {
        readonly List<ConfigureEvent> _items;

        public ConfigureEventRepository(ConfigureEvent item)
        {
            _items = [item];
        }

        public Task Create(ConfigureEvent item)
        {
            _items.Add(item);
            return Task.CompletedTask;
        }

        public Task<ConfigureEvent?> Read(Expression<Func<ConfigureEvent, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult(_items.FirstOrDefault(predicate));
        }

        public Task<ConfigureEvent?> Read(int id)
        {
            return Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        }

        public Task<IEnumerable<ConfigureEvent>> ReadMany()
        {
            return Task.FromResult<IEnumerable<ConfigureEvent>>(_items.ToList());
        }

        public Task<IEnumerable<ConfigureEvent>> ReadMany(Expression<Func<ConfigureEvent, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<ConfigureEvent>>(_items.Where(predicate).ToList());
        }

        public Task Update(ConfigureEvent item)
        {
            var index = _items.FindIndex(x => x.Id == item.Id);
            if (index >= 0)
            {
                _items[index] = item;
            }
            return Task.CompletedTask;
        }

        public Task Delete(int id)
        {
            _items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task Delete(ConfigureEvent item)
        {
            _items.Remove(item);
            return Task.CompletedTask;
        }

        public Task DeleteMany(Expression<Func<ConfigureEvent, bool>> filter)
        {
            var predicate = filter.Compile();
            _items.RemoveAll(x => predicate(x));
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<ConfigureEvent> items)
        {
            var set = items.ToHashSet();
            _items.RemoveAll(x => set.Contains(x));
            return Task.CompletedTask;
        }
    }
}
