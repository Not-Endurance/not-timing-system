using NTS.Application.Factories;
using NTS.Contracts.Core.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Aggregates.Results;
using NTS.Domain.Core.Objects.Rankers;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using ConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using CoreCompetition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;
using CoreParticipation = NTS.Domain.Core.Aggregates.Participation;
using CorePhase = NTS.Domain.Core.Aggregates.Participations.Entities.Phase;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupLoop = NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop;
using SetupPhase = NTS.Domain.Setup.Aggregates.ConfigureEvents.Phase;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// The rules of the Regional competitions of a Tenant (ADR-0012): how loop speed is judged and which ranker applies.
/// They are fields of the Tenant, copied into the Event when it starts, and the Phases and Results of the Event read
/// them from there. Nothing is read from a static, which cannot work in one process that serves several Tenants.
/// </summary>
public sealed class RegionalRulesTests
{
    static readonly DateTimeOffset START = new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly Country BULGARIA = new(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");

    // A 20 km Phase from 08:00 that arrives at 09:00 and is presented at 09:10: 20 km in an hour of loop, in 70 minutes
    // of Phase. Worked out by hand: 20 km/h on the loop and 20 / (70 / 60) = 17.142857... on the Phase.
    [Theory]
    [InlineData(true, 20.0)]
    [InlineData(false, 17.142857142857142)]
    public void A_Regional_Phase_is_judged_on_the_loop_speed_only_when_the_rules_of_its_Event_say_so(
        bool onlyAverageLoopSpeed,
        double expected
    )
    {
        var phase = CreatePhase(CompetitionRuleset.Regional, isFinal: false);

        var speed = phase.GetAverageSpeed(new RegionalRules(onlyAverageLoopSpeed));

        Assert.Equal(expected, speed!.ToDouble(), 9);
    }

    [Fact]
    public void The_rule_is_for_Regional_competitions_only()
    {
        var phase = CreatePhase(CompetitionRuleset.FEI, isFinal: false);

        var speed = phase.GetAverageSpeed(new RegionalRules(onlyAverageLoopSpeed: true));

        Assert.Equal(20 / (70.0 / 60), speed!.ToDouble(), 9);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_final_Phase_has_only_its_loop_to_judge_whatever_the_rules(bool onlyAverageLoopSpeed)
    {
        var phase = CreatePhase(CompetitionRuleset.Regional, isFinal: true);

        var speed = phase.GetAverageSpeed(new RegionalRules(onlyAverageLoopSpeed));

        Assert.Equal(20.0, speed!.ToDouble(), 9);
    }

    [Fact]
    public void A_Phase_is_judged_by_the_rules_it_is_asked_with_and_by_nothing_it_remembers()
    {
        var phase = CreatePhase(CompetitionRuleset.Regional, isFinal: false);
        var onTheLoop = new RegionalRules(onlyAverageLoopSpeed: true);
        var onThePhase = new RegionalRules(onlyAverageLoopSpeed: false);

        Assert.Equal(20.0, phase.GetAverageSpeed(onTheLoop)!.ToDouble(), 9);
        Assert.Equal(20 / (70.0 / 60), phase.GetAverageSpeed(onThePhase)!.ToDouble(), 9);
        Assert.Equal(20.0, phase.GetAverageSpeed(onTheLoop)!.ToDouble(), 9);
    }

    [Fact]
    public void A_Phase_asked_without_rules_is_judged_as_one_whose_Event_has_none()
    {
        var phase = CreatePhase(CompetitionRuleset.Regional, isFinal: false);

        Assert.Equal(20 / (70.0 / 60), phase.GetAverageSpeed()!.ToDouble(), 9);
        Assert.Equal(20 / (70.0 / 60), phase.GetAverageSpeed(RegionalRules.None)!.ToDouble(), 9);
    }

    [Theory]
    [InlineData(CompetitionRuleset.Regional, "BG", "BG")]
    [InlineData(CompetitionRuleset.Regional, null, null)]
    [InlineData(CompetitionRuleset.FEI, "BG", null)]
    [InlineData(CompetitionRuleset.FEI, null, null)]
    public void The_ranker_a_competition_is_ranked_by_is_the_choice_of_its_Event_for_Regional_ones_and_the_FEI_ranker_for_the_rest(
        CompetitionRuleset ruleset,
        string? chosen,
        string? expected
    )
    {
        var rules = new RegionalRules(onlyAverageLoopSpeed: false, rankerCode: chosen);

        Assert.Equal(expected, rules.RankerFor(ruleset));
    }

    [Fact]
    public void A_Regional_Result_is_ranked_by_the_regional_ranker_its_Event_chose_and_by_the_FEI_ranker_otherwise()
    {
        var (early, late) = TwoParticipations();
        var ranking = CreateRanking([new RankingEntry(late.Id, false), new RankingEntry(early.Id, false)]);
        var rankers = new Ranker[] { new LastNumberFirstRanker("ZZ") };

        var byTheChosenOne = new Result(ranking, [early, late], new RegionalRules(false, rankerCode: "ZZ"), rankers);
        var byNone = new Result(ranking, [early, late], RegionalRules.None, rankers);
        var byOneThereIsNo = new Result(ranking, [early, late], new RegionalRules(false, rankerCode: "YY"), rankers);

        Assert.Equal([late.Id, early.Id], byTheChosenOne.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], byTheChosenOne.Entries.Select(x => x.Rank));
        Assert.Equal([early.Id, late.Id], byNone.Entries.Select(x => x.ParticipationId));
        Assert.Equal([early.Id, late.Id], byOneThereIsNo.Entries.Select(x => x.ParticipationId));
    }

    [Fact]
    public void An_FEI_Result_is_ranked_by_the_FEI_ranker_whatever_its_Event_chose_for_Regional_ones()
    {
        var (early, late) = TwoParticipations();
        var ranking = CreateRanking(
            [new RankingEntry(late.Id, false), new RankingEntry(early.Id, false)],
            CompetitionRuleset.FEI
        );
        var rankers = new Ranker[] { new LastNumberFirstRanker("ZZ") };

        var result = new Result(ranking, [early, late], new RegionalRules(false, rankerCode: "ZZ"), rankers);

        Assert.Equal([early.Id, late.Id], result.Entries.Select(x => x.ParticipationId));
    }

    [Fact]
    public void A_Result_composed_without_rules_is_ranked_by_the_FEI_ranker()
    {
        var (early, late) = TwoParticipations();
        var ranking = CreateRanking([new RankingEntry(late.Id, false), new RankingEntry(early.Id, false)]);

        var result = new Result(ranking, [early, late]);

        Assert.Equal([early.Id, late.Id], result.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], result.Entries.Select(x => x.Rank));
    }

    [Fact]
    public void Rules_are_values_two_with_the_same_fields_are_the_same_rules()
    {
        Assert.Equal(new RegionalRules(true, "BG"), new RegionalRules(true, "BG"));
        Assert.NotEqual(new RegionalRules(true, "BG"), new RegionalRules(false, "BG"));
        Assert.NotEqual(new RegionalRules(true, "BG"), new RegionalRules(true, null));
        Assert.Equal(RegionalRules.None, new RegionalRules(false));
    }

    [Fact]
    public void A_ranker_code_is_text_of_one_line_or_none()
    {
        Assert.Throws<ArgumentException>(() => new RegionalRules(false, rankerCode: " "));
        Assert.Throws<ArgumentException>(() => new RegionalRules(false, rankerCode: "B\nG"));
        Assert.Throws<ArgumentException>(() => new RegionalRules(false, rankerCode: new string('B', 41)));
    }

    [Fact]
    public void A_Tenant_has_no_rules_until_they_are_set()
    {
        var tenant = Tenant.ForCountry(BULGARIA);

        Assert.Equal(RegionalRules.None, tenant.RegionalRules);
    }

    [Fact]
    public void Setting_the_rules_of_a_Tenant_makes_another_Tenant_and_leaves_the_first_as_it_was()
    {
        var tenant = Tenant.ForCountry(BULGARIA);

        var changed = tenant.WithRules(new RegionalRules(true));

        Assert.Equal(new RegionalRules(true), changed.RegionalRules);
        Assert.Equal(RegionalRules.None, tenant.RegionalRules);
        Assert.Equal(tenant.Id, changed.Id);
        Assert.Equal(tenant.Name, changed.Name);
        Assert.Equal(tenant.Kind, changed.Kind);
    }

    [Fact]
    public void An_Event_copies_the_rules_of_its_Tenant_when_it_starts()
    {
        var tenant = Tenant.ForCountry(BULGARIA).WithRules(new RegionalRules(true, "BG"));

        var started = EventInformationFactory.Create(CreateSetupEvent(), tenant.RegionalRules);

        Assert.Equal(new RegionalRules(true, "BG"), started.RegionalRules);
    }

    [Fact]
    public void A_later_change_of_the_rules_of_the_Tenant_does_not_change_an_Event_that_has_started()
    {
        var tenant = Tenant.ForCountry(BULGARIA).WithRules(new RegionalRules(true));
        var started = EventInformationFactory.Create(CreateSetupEvent(), tenant.RegionalRules);

        var edited = tenant.WithRules(new RegionalRules(false, "BG"));
        var startedAfter = EventInformationFactory.Create(CreateSetupEvent(), edited.RegionalRules);

        Assert.Equal(new RegionalRules(true), started.RegionalRules);
        Assert.Equal(new RegionalRules(false, "BG"), startedAfter.RegionalRules);
    }

    [Fact]
    public void An_Event_started_without_the_rules_of_a_Tenant_has_none()
    {
        var started = EventInformationFactory.Create(CreateSetupEvent());

        Assert.Equal(RegionalRules.None, started.RegionalRules);
    }

    [Fact]
    public void The_rules_of_an_Event_are_kept_by_the_model_it_is_stored_as()
    {
        var started = EventInformationFactory.Create(CreateSetupEvent(), new RegionalRules(true, "BG"));

        var stored = EventInformationModel.From(started);
        var loaded = stored.MapToEntity();

        Assert.Equal(new RegionalRules(true, "BG"), loaded.RegionalRules);
    }

    [Fact]
    public void An_Event_stored_before_there_were_rules_loads_with_none()
    {
        var stored = EventInformationModel.From(EventInformationFactory.Create(CreateSetupEvent()));
        stored.RegionalRules = null;

        Assert.Equal(RegionalRules.None, stored.MapToEntity().RegionalRules);
    }

    static ConfigureEvent CreateSetupEvent()
    {
        var loop = new SetupLoop(40, id: TestId.Of(4));
        var competition = new SetupCompetition(
            name: "Regional",
            ruleset: CompetitionRuleset.Regional,
            start: START,
            compulsoryThresholdSpan: null,
            minSpeedRestriction: null,
            maxSpeedRestriction: null,
            feiEventId: null,
            feiEventCode: null,
            feiCompetitionId: null,
            feiRule: null,
            feiScheduleNumber: null,
            phases: [new SetupPhase(loop, recovery: 40, rest: null, id: TestId.Of(5))],
            participations: [],
            id: TestId.Of(7)
        );

        return new ConfigureEvent("Event", "Sofia", BULGARIA, null, [competition], [], [loop], [], id: TestId.Of(300));
    }

    static CorePhase CreatePhase(CompetitionRuleset ruleset, bool isFinal)
    {
        return new CorePhase(
            "GATE1",
            20,
            40,
            null,
            ruleset,
            isFinal,
            null,
            Timestamp.Create(START),
            Timestamp.Create(START.AddHours(1)),
            Timestamp.Create(START.AddHours(1).AddMinutes(10)),
            null,
            false,
            false,
            false
        );
    }

    static Ranking CreateRanking(
        IEnumerable<RankingEntry> entries,
        CompetitionRuleset ruleset = CompetitionRuleset.Regional
    )
    {
        return new Ranking(
            "CEI 1*",
            ruleset,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            eventId: TestId.Of(10),
            id: TestId.Of(99)
        );
    }

    static (CoreParticipation Early, CoreParticipation Late) TwoParticipations()
    {
        var now = DateTimeOffset.Now;
        return (
            CreateParticipation(1, now.AddHours(-4), now.AddHours(-3)),
            CreateParticipation(2, now.AddHours(-3), now.AddHours(-2))
        );
    }

    static CoreParticipation CreateParticipation(int number, DateTimeOffset start, DateTimeOffset arrive)
    {
        var athlete = new Athlete($"Athlete {number}", null, BULGARIA, null, null, TestId.Of(number));
        var horse = new Horse($"Horse {number}", null, null, TestId.Of(number));
        var combination = new Combination(number, athlete, horse, null, "20", null, null, TestId.Of(number));
        var phase = new CorePhase(
            "GATE1",
            20,
            40,
            null,
            CompetitionRuleset.Regional,
            true,
            null,
            Timestamp.Create(start),
            Timestamp.Create(arrive),
            Timestamp.Create(arrive.AddMinutes(5)),
            null,
            false,
            false,
            false
        );

        return new CoreParticipation(
            ParticipationCategory.Senior,
            new CoreCompetition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection([phase]),
            null,
            eventId: TestId.Of(10),
            id: TestId.Of(number)
        );
    }

    /// <summary>A regional ranker for a test: the highest start number first. The FEI ranker puts the earliest arrival first.</summary>
    sealed class LastNumberFirstRanker : Ranker
    {
        public LastNumberFirstRanker(string code)
        {
            CountryIsoCode = code;
        }

        public override List<ParticipationResult> Rank(IEnumerable<ParticipationResult> entries)
        {
            return [.. entries.OrderByDescending(x => x.Participation.Combination.Number)];
        }
    }
}
