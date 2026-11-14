using NoTiming.Application.Factories;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;
using SetupCompetition = NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupParticipation = NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Participation;

namespace NoTiming.Application.Tests.Factories;

public sealed class ParticipationAndRankingFactoryTests
{
    [Fact]
    public void Create_WhenCompetitionHasSpeedRestrictions_ShouldApplyThemToParticipations()
    {
        var competition = CreateCompetition(Restrictions(phaseAverageMin: 10, phaseAverageMax: 16), Restrictions());

        var (participations, _) = ParticipationAndRankingFactory.Create(competition, [], eventId: 100);

        var restrictions = Assert.Single(participations).Combination.Restrictions;
        Assert.Equal(10, restrictions.PhaseAverageSpeedMin);
        Assert.Equal(16, restrictions.PhaseAverageSpeedMax);
    }

    [Fact]
    public void Create_WhenParticipationHasStricterLimits_ShouldUseTheStricterLimit()
    {
        var competition = CreateCompetition(
            Restrictions(phaseAverageMin: 10, phaseAverageMax: 16),
            Restrictions(phaseAverageMin: 12, phaseAverageMax: 14)
        );

        var (participations, _) = ParticipationAndRankingFactory.Create(competition, [], eventId: 100);

        var restrictions = Assert.Single(participations).Combination.Restrictions;
        Assert.Equal(12, restrictions.PhaseAverageSpeedMin);
        Assert.Equal(14, restrictions.PhaseAverageSpeedMax);
    }

    [Fact]
    public void Create_WhenParticipationWouldRelaxCompetitionLimit_ShouldKeepCompetitionLimit()
    {
        var competition = CreateCompetition(
            Restrictions(phaseAverageMin: 10, phaseAverageMax: 12),
            Restrictions(phaseAverageMin: 8, phaseAverageMax: 16)
        );

        var (participations, _) = ParticipationAndRankingFactory.Create(competition, [], eventId: 100);

        var restrictions = Assert.Single(participations).Combination.Restrictions;
        Assert.Equal(10, restrictions.PhaseAverageSpeedMin);
        Assert.Equal(12, restrictions.PhaseAverageSpeedMax);
    }

    [Fact]
    public void Create_WhenParticipationHasLoopLimit_ShouldKeepLoopLimitOnTheCombination()
    {
        var competition = CreateCompetition(Restrictions(), Restrictions(loopMax: 16));

        var (participations, _) = ParticipationAndRankingFactory.Create(competition, [], eventId: 100);

        var restrictions = Assert.Single(participations).Combination.Restrictions;
        Assert.Equal(16, restrictions.LoopSpeedMax);
        Assert.Null(restrictions.PhaseAverageSpeedMax);
    }

    [Fact]
    public void Create_WhenNoSpeedRestrictions_ShouldLeaveAllLimitsEmpty()
    {
        var competition = CreateCompetition(Restrictions(), Restrictions());

        var (participations, _) = ParticipationAndRankingFactory.Create(competition, [], eventId: 100);

        var restrictions = Assert.Single(participations).Combination.Restrictions;
        Assert.Equal(SpeedRestrictions.None, restrictions);
    }

    static SpeedRestrictions Restrictions(
        double? loopMax = null,
        double? phaseAverageMin = null,
        double? phaseAverageMax = null
    )
    {
        return new SpeedRestrictions(null, loopMax, null, null, null, null, phaseAverageMin, phaseAverageMax);
    }

    static SetupCompetition CreateCompetition(
        SpeedRestrictions competitionRestrictions,
        SpeedRestrictions participationRestrictions
    )
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Rider", "Rider", null, country, null, id: 1);
        var horse = new Horse("Horse", "Horse", null, id: 2);
        var combination = new Combination(1, athlete, horse, id: 3);
        var participation = new SetupParticipation(
            isNotRanked: false,
            combination: combination,
            category: ParticipationCategory.Senior,
            startTimeOverride: null,
            restrictions: participationRestrictions,
            id: 6
        );
        var phase = new Phase(new Loop(40, id: 4), recovery: 40, rest: null, id: 5);

        return new SetupCompetition(
            name: "Speed defaults",
            englishNames: false,
            start: new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero),
            compulsoryThresholdSpan: null,
            speedRestrictions: competitionRestrictions,
            feiEventId: null,
            feiEventCode: null,
            feiCompetitionId: null,
            feiRule: null,
            feiScheduleNumber: null,
            phases: [phase],
            participations: [participation],
            id: 7
        );
    }
}
