using NTS.Application.Factories;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupParticipation = NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation;

namespace NTS.Tests.Unit.Application;

public sealed class ParticipationAndRankingFactoryTests
{
    [Fact]
    public void Create_AppliesCompetitionSpeedRestrictionsByDefault()
    {
        var competition = CreateCompetition(minSpeedRestriction: 10, maxSpeedRestriction: 16);

        var (participations, _) = ParticipationAndRankingFactory.Create(competition, [], eventId: TestId.Of(100));

        var combination = Assert.Single(participations).Combination;
        Assert.Equal(10, combination.MinAverageSpeed);
        Assert.Equal(16, combination.MaxAverageSpeed);
    }

    [Fact]
    public void Create_UsesParticipationSpeedOverridesOverCompetitionRestrictions()
    {
        var competition = CreateCompetition(
            minSpeedRestriction: 10,
            maxSpeedRestriction: 16,
            minSpeedOverride: 8,
            maxSpeedOverride: 12
        );

        var (participations, _) = ParticipationAndRankingFactory.Create(competition, [], eventId: TestId.Of(100));

        var combination = Assert.Single(participations).Combination;
        Assert.Equal(8, combination.MinAverageSpeed);
        Assert.Equal(12, combination.MaxAverageSpeed);
    }

    [Fact]
    public void Create_LeavesSpeedRestrictionsEmptyWhenNoDefaultsOrOverridesExist()
    {
        var competition = CreateCompetition(minSpeedRestriction: null, maxSpeedRestriction: null);

        var (participations, _) = ParticipationAndRankingFactory.Create(competition, [], eventId: TestId.Of(100));

        var combination = Assert.Single(participations).Combination;
        Assert.Null(combination.MinAverageSpeed);
        Assert.Null(combination.MaxAverageSpeed);
    }

    [Fact]
    public void Create_AllowsConflictingSpeedRestrictions()
    {
        var competition = CreateCompetition(minSpeedRestriction: 16, maxSpeedRestriction: 10);

        var (participations, _) = ParticipationAndRankingFactory.Create(competition, [], eventId: TestId.Of(100));

        var combination = Assert.Single(participations).Combination;
        Assert.Equal(16, combination.MinAverageSpeed);
        Assert.Equal(10, combination.MaxAverageSpeed);
    }

    [Fact]
    public void Create_copies_the_Guids_of_the_Setup_Athlete_Horse_and_Combination_into_the_Core_copies()
    {
        var competition = CreateCompetition(minSpeedRestriction: null, maxSpeedRestriction: null);
        var setupCombination = Assert.Single(competition.Participations).Combination;

        var (participations, _) = ParticipationAndRankingFactory.Create(competition, [], eventId: TestId.Of(100));

        var combination = Assert.Single(participations).Combination;
        Assert.Equal(setupCombination.Athlete.Id, combination.Athlete.Id);
        Assert.Equal(setupCombination.Horse.Id, combination.Horse.Id);
        Assert.Equal(setupCombination.Id, combination.Id);
    }

    static SetupCompetition CreateCompetition(
        double? minSpeedRestriction,
        double? maxSpeedRestriction,
        double? minSpeedOverride = null,
        double? maxSpeedOverride = null
    )
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Rider", "Rider", null, country, null, id: TestId.Of(1));
        var horse = new Horse("Horse", "Horse", null, id: TestId.Of(2));
        var combination = new Combination(1, athlete, horse, id: TestId.Of(3));
        var phase = new Phase(new Loop(40, id: TestId.Of(4)), recovery: 40, rest: null, id: TestId.Of(5));
        var participation = new SetupParticipation(
            isNotRanked: false,
            combination: combination,
            category: ParticipationCategory.Senior,
            startTimeOverride: null,
            maxSpeedOverride: maxSpeedOverride,
            minSpeedOverride: minSpeedOverride,
            id: TestId.Of(6)
        );

        return new SetupCompetition(
            name: "Speed defaults",
            ruleset: CompetitionRuleset.Regional,
            start: new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero),
            compulsoryThresholdSpan: null,
            minSpeedRestriction: minSpeedRestriction,
            maxSpeedRestriction: maxSpeedRestriction,
            feiEventId: null,
            feiEventCode: null,
            feiCompetitionId: null,
            feiRule: null,
            feiScheduleNumber: null,
            phases: [phase],
            participations: [participation],
            id: TestId.Of(7)
        );
    }
}
