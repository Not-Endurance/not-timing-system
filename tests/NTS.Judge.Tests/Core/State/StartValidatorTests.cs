using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Judge.Tests.Core.State;

public class StartValidatorTests
{
    [Fact]
    public void Validate_WhenDuplicateParticipationHasSamePhaseConfiguration_ReturnsNoIssues()
    {
        var setupEvent = CreateEvent(
            [
                (40d, 40, 30),
                (40d, 50, null),
            ],
            [
                (40d, 40, 30),
                (40d, 50, null),
            ]
        );

        var result = StartValidator.Validate(setupEvent);

        Assert.Empty(result.Data ?? []);
    }

    [Fact]
    public void Validate_WhenDuplicateParticipationHasDifferentPhaseConfiguration_ReturnsIssues()
    {
        var setupEvent = CreateEvent(
            [
                (40d, 40, 30),
                (40d, 50, null),
            ],
            [
                (35d, 40, 30),
                (45d, 50, null),
            ]
        );

        var result = StartValidator.Validate(setupEvent);

        var issue = Assert.Single(result.Data ?? []);
        Assert.Equal(12, issue.ParticipationNumber);
        Assert.Equal("John Doe", issue.AthleteName);
        Assert.Equal("Horse", issue.HorseName);
        Assert.Equal(2, issue.Competitions.Count);
        Assert.Contains(issue.Competitions, x => x.CompetitionName == "Competition A");
        Assert.Contains(issue.Competitions, x => x.CompetitionName == "Competition B");
    }

    static UpcomingEvent CreateEvent(
        (double Distance, int Recovery, int? RestMinutes)[] competitionOnePhases,
        (double Distance, int Recovery, int? RestMinutes)[] competitionTwoPhases
    )
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["John", "Doe"]), null, country, null, 1);
        var horse = new Horse("Horse", null, 1);
        var combination = new Combination(12, athlete, horse, 1);

        var competitionOne = CreateCompetition("Competition A", competitionOnePhases, combination, 1);
        var competitionTwo = CreateCompetition("Competition B", competitionTwoPhases, combination, 2);
        var loops = new List<Loop>
        {
            new(100, 100),
            new(101, 101),
        };

        return new UpcomingEvent(
            "Event",
            "Sofia",
            country,
            null,
            null,
            null,
            [competitionOne, competitionTwo],
            [],
            loops,
            [combination],
            1
        );
    }

    static Competition CreateCompetition(
        string name,
        IEnumerable<(double Distance, int Recovery, int? RestMinutes)> phaseConfig,
        Combination combination,
        int id
    )
    {
        var phases = phaseConfig
            .Select((x, index) => new Phase(new Loop(x.Distance, index + 1), x.Recovery, x.RestMinutes, index + 1))
            .ToList();
        var participation = new Participation(
            isNotRanked: false,
            combination: combination,
            category: ParticipationCategory.Senior,
            startTimeOverride: null,
            maxSpeedOverride: null,
            minSpeedOverride: null,
            id: id
        );

        return new Competition(
            name,
            CompetitionType.Qualification,
            CompetitionRuleset.FEI,
            DateTimeOffset.UtcNow.AddHours(id),
            null,
            null,
            null,
            null,
            phases,
            [participation],
            id
        );
    }
}
