using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Judge.Tests.Core.State;

public class StartValidatorTests
{
    [Fact]
    public void Validate_WhenDuplicateParticipationHasSamePhaseConfiguration_ReturnsNoIssues()
    {
        var setupEvent = CreateEvent([(40d, 40, 30), (40d, 50, null)], [(40d, 40, 30), (40d, 50, null)]);

        var result = StartValidator.Validate(setupEvent);

        Assert.Empty(result.Data ?? []);
    }

    [Fact]
    public void Validate_WhenDuplicateParticipationHasDifferentPhaseConfiguration_ReturnsIssues()
    {
        var setupEvent = CreateEvent([(40d, 40, 30), (40d, 50, null)], [(35d, 40, 30), (45d, 50, null)]);

        var result = StartValidator.Validate(setupEvent);

        var issue = Assert.Single(result.Data ?? []);
        Assert.Equal(12, issue.ParticipationNumber);
        Assert.Equal("John Doe", issue.AthleteName);
        Assert.Equal("Horse", issue.HorseName);
        Assert.Equal(2, issue.Competitions.Count);
        Assert.Contains(issue.Competitions, x => x.CompetitionName == "Competition A");
        Assert.Contains(issue.Competitions, x => x.CompetitionName == "Competition B");
    }

    [Fact]
    public void Validate_WhenEventHasNoCompetitions_ReturnsIssue()
    {
        var setupEvent = CreateEvent([], []);

        var result = StartValidator.Validate(
            new ConfigureEvent(
                setupEvent.Name,
                setupEvent.Location,
                setupEvent.Country,
                setupEvent.ShowFeiId,
                setupEvent.FeiId,
                setupEvent.FeiEventCode,
                [],
                setupEvent.Officials,
                setupEvent.Loops,
                setupEvent.Combinations,
                setupEvent.Id
            )
        );

        var issue = Assert.Single(result.Data ?? []);
        Assert.Equal("At least one competition must be configured before start.", issue.Summary);
        Assert.False(issue.IsAutoCorrectable);
    }

    [Fact]
    public void Validate_WhenCompetitionHasNoPhases_ReturnsIssue()
    {
        var setupEvent = CreateEvent([(40d, 40, null)], [(40d, 40, null)]);
        var competition = setupEvent.Competitions[0];
        var invalidCompetition = new Competition(
            competition.Name,
            competition.Type,
            competition.Ruleset,
            competition.Start,
            competition.CompulsoryThresholdSpan,
            competition.FeiId,
            competition.FeiRule,
            competition.FeiScheduleNumber,
            [],
            competition.Participations,
            competition.Id
        );

        var result = StartValidator.Validate(ReplaceCompetition(setupEvent, invalidCompetition, 0));

        var issue = Assert.Single((result.Data ?? []).Where(x => x.Summary.Contains("must have at least one phase")));
        Assert.Contains("must have at least one phase", issue.Summary);
        Assert.False(issue.IsAutoCorrectable);
    }

    [Fact]
    public void Validate_WhenCompetitionHasNoParticipations_ReturnsIssue()
    {
        var setupEvent = CreateEvent([(40d, 40, null)], [(40d, 40, null)]);
        var competition = setupEvent.Competitions[0];
        var invalidCompetition = new Competition(
            competition.Name,
            competition.Type,
            competition.Ruleset,
            competition.Start,
            competition.CompulsoryThresholdSpan,
            competition.FeiId,
            competition.FeiRule,
            competition.FeiScheduleNumber,
            competition.Phases,
            [],
            competition.Id
        );

        var result = StartValidator.Validate(ReplaceCompetition(setupEvent, invalidCompetition, 0));

        var issue = Assert.Single(result.Data ?? []);
        Assert.Contains("must have at least one participation", issue.Summary);
        Assert.False(issue.IsAutoCorrectable);
    }

    [Fact]
    public void Validate_WhenNonFinalPhaseHasNoRest_ReturnsIssue()
    {
        var setupEvent = CreateEvent([(40d, 40, null), (40d, 50, null)], [(40d, 40, 30), (40d, 50, null)]);

        var result = StartValidator.Validate(setupEvent);

        var issue = Assert.Single(
            (result.Data ?? []).Where(x => x.Summary.Contains("non-final phases without rest: 1"))
        );
        Assert.Contains("non-final phases without rest: 1", issue.Summary);
        Assert.False(issue.IsAutoCorrectable);
    }

    static ConfigureEvent CreateEvent(
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
        var loops = new List<Loop> { new(100, 100), new(101, 101) };

        return new ConfigureEvent(
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

    static ConfigureEvent ReplaceCompetition(ConfigureEvent setupEvent, Competition competition, int index)
    {
        var competitions = setupEvent.Competitions.ToList();
        competitions[index] = competition;
        return new ConfigureEvent(
            setupEvent.Name,
            setupEvent.Location,
            setupEvent.Country,
            setupEvent.ShowFeiId,
            setupEvent.FeiId,
            setupEvent.FeiEventCode,
            competitions,
            setupEvent.Officials,
            setupEvent.Loops,
            setupEvent.Combinations,
            setupEvent.Id
        );
    }
}
