using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Tests.Domain;

public class CompetitionSpeedLimitTests
{
    [Fact]
    public void Constructor_WhenCompetitionIsStar_ClearsInheritedMaxSpeedLimit()
    {
        var participation = new Participation(
            isNotRanked: false,
            combination: CreateCombination(41),
            category: ParticipationCategory.Senior,
            startTimeOverride: null,
            maxSpeedOverride: null,
            minSpeedOverride: null,
            minAverageSpeed: 10,
            maxAverageSpeed: 16,
            id: 101
        );

        _ = new Competition(
            name: "CEI 2*",
            type: CompetitionType.Star,
            ruleset: CompetitionRuleset.FEI,
            start: DateTimeOffset.UtcNow,
            compulsoryThresholdSpan: null,
            feiId: null,
            feiRule: null,
            feiScheduleNumber: null,
            phases: [new Phase(new Loop(40, 301), 40, null, 201)],
            participations: [participation],
            id: 401
        );

        Assert.Equal(10, participation.MinAverageSpeed);
        Assert.Null(participation.MaxAverageSpeed);
    }

    [Fact]
    public void Constructor_WhenParticipationIsTraining_ClearsDefaultSpeedLimits()
    {
        var participation = new Participation(
            isNotRanked: false,
            combination: CreateCombination(42),
            category: ParticipationCategory.Training,
            startTimeOverride: null,
            maxSpeedOverride: null,
            minSpeedOverride: null,
            id: 102
        );

        _ = new Competition(
            name: "Training",
            type: CompetitionType.Qualification,
            ruleset: CompetitionRuleset.Regional,
            start: DateTimeOffset.UtcNow,
            compulsoryThresholdSpan: null,
            feiId: null,
            feiRule: null,
            feiScheduleNumber: null,
            phases: [new Phase(new Loop(40, 302), 40, null, 202)],
            participations: [participation],
            id: 402
        );

        Assert.Null(participation.MinAverageSpeed);
        Assert.Null(participation.MaxAverageSpeed);
    }

    [Fact]
    public void Constructor_WhenTrainingParticipationHasOverrides_PreservesThem()
    {
        var participation = new Participation(
            isNotRanked: false,
            combination: CreateCombination(43),
            category: ParticipationCategory.Training,
            startTimeOverride: null,
            maxSpeedOverride: 16,
            minSpeedOverride: 10,
            id: 103
        );

        _ = new Competition(
            name: "Training",
            type: CompetitionType.Qualification,
            ruleset: CompetitionRuleset.Regional,
            start: DateTimeOffset.UtcNow,
            compulsoryThresholdSpan: null,
            feiId: null,
            feiRule: null,
            feiScheduleNumber: null,
            phases: [new Phase(new Loop(40, 303), 40, null, 203)],
            participations: [participation],
            id: 403
        );

        Assert.Equal(10, participation.MinAverageSpeed);
        Assert.Equal(16, participation.MaxAverageSpeed);
    }

    static Combination CreateCombination(int number)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["Speed", "Test"]), null, country, null, 11);
        var horse = new Horse("Horse", null, 12);
        return new Combination(number, athlete, horse, 13);
    }
}
