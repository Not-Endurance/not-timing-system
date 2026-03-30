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

    static Combination CreateCombination(int number)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["Speed", "Test"]), null, country, null, 11);
        var horse = new Horse("Horse", null, 12);
        return new Combination(number, athlete, horse, 13);
    }
}
