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

    [Fact]
    public void Create_makes_the_Ranking_entries_reference_the_new_Participations_with_the_mark_of_the_Setup()
    {
        var competition = CreateCompetition(
            minSpeedRestriction: null,
            maxSpeedRestriction: null,
            notRanked: [false, true]
        );

        var (participations, entriesByCategory) = ParticipationAndRankingFactory.Create(
            competition,
            [],
            eventId: TestId.Of(100)
        );

        var entries = Assert.Single(entriesByCategory).Value;
        Assert.Equal(participations.Select(x => x.Id), entries.Select(x => x.ParticipationId));
        Assert.Equal([false, true], entries.Select(x => x.IsNotRanked));
        Assert.All(entries, x => Assert.Null(x.Rank));
    }

    [Fact]
    public void Create_references_the_Participation_a_Combination_already_has_in_the_Event_with_a_mark_of_its_own()
    {
        var first = CreateCompetition(minSpeedRestriction: null, maxSpeedRestriction: null, notRanked: [false]);
        var second = CreateCompetition(minSpeedRestriction: null, maxSpeedRestriction: null, notRanked: [true]);
        var (existing, firstEntries) = ParticipationAndRankingFactory.Create(first, [], eventId: TestId.Of(100));

        var (added, secondEntries) = ParticipationAndRankingFactory.Create(second, existing, eventId: TestId.Of(100));

        Assert.Empty(added);
        var inTheFirst = Assert.Single(Assert.Single(firstEntries).Value);
        var inTheSecond = Assert.Single(Assert.Single(secondEntries).Value);
        Assert.Equal(Assert.Single(existing).Id, inTheFirst.ParticipationId);
        Assert.Equal(inTheFirst.ParticipationId, inTheSecond.ParticipationId);
        Assert.False(inTheFirst.IsNotRanked);
        Assert.True(inTheSecond.IsNotRanked);
    }

    static SetupCompetition CreateCompetition(
        double? minSpeedRestriction,
        double? maxSpeedRestriction,
        double? minSpeedOverride = null,
        double? maxSpeedOverride = null,
        bool[]? notRanked = null
    )
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var phase = new Phase(new Loop(40, id: TestId.Of(4)), recovery: 40, rest: null, id: TestId.Of(5));
        var participations = (notRanked ?? [false])
            .Select(
                (isNotRanked, index) =>
                    CreateSetupParticipation(country, index + 1, isNotRanked, minSpeedOverride, maxSpeedOverride)
            )
            .ToList();

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
            participations: participations,
            id: TestId.Of(7)
        );
    }

    static SetupParticipation CreateSetupParticipation(
        Country country,
        int number,
        bool isNotRanked,
        double? minSpeedOverride,
        double? maxSpeedOverride
    )
    {
        var athlete = new Athlete("Rider", "Rider", null, country, null, id: TestId.Of(number));
        var horse = new Horse("Horse", "Horse", null, id: TestId.Of(100 + number));
        var combination = new Combination(number, athlete, horse, id: TestId.Of(200 + number));

        return new SetupParticipation(
            isNotRanked: isNotRanked,
            combination: combination,
            category: ParticipationCategory.Senior,
            startTimeOverride: null,
            maxSpeedOverride: maxSpeedOverride,
            minSpeedOverride: minSpeedOverride,
            id: TestId.Of(300 + number)
        );
    }
}
