using NTS.Application.Factories;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using SetupParticipation = NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation;

namespace NTS.Judge.Tests.Core;

public class ParticipationAndRankingFactoryTests
{
    [Fact]
    public void CreateParticipation_WhenStartTimeOverrideIsProvided_UsesOverrideForPhase1AndLeavesLaterPhasesUnset()
    {
        var competitionStart = DateTimeOffset.Now.AddHours(-2);
        var overrideStart = DateTimeOffset.Now.AddHours(1);
        var setupParticipation = CreateSetupParticipation(overrideStart);
        var setupCompetition = CreateCompetition(competitionStart, setupParticipation, phaseCount: 2);

        var participation = ParticipationAndRankingFactory.CreateParticipation(
            setupCompetition,
            setupParticipation,
            14
        );

        Assert.Equal(overrideStart.ToUniversalTime(), participation.Phases[0].StartTime?.ToDateTimeOffset());
        Assert.Null(participation.Phases[1].StartTime);
    }

    [Fact]
    public void CreateParticipation_WhenStartTimeOverrideMissing_UsesCompetitionStartForPhase1()
    {
        var competitionStart = DateTimeOffset.Now.AddHours(1);
        var setupParticipation = CreateSetupParticipation(null);
        var setupCompetition = CreateCompetition(competitionStart, setupParticipation, phaseCount: 2);

        var participation = ParticipationAndRankingFactory.CreateParticipation(
            setupCompetition,
            setupParticipation,
            14
        );

        Assert.Equal(competitionStart.ToUniversalTime(), participation.Phases[0].StartTime?.ToDateTimeOffset());
        Assert.Null(participation.Phases[1].StartTime);
    }

    static Competition CreateCompetition(
        DateTimeOffset competitionStart,
        SetupParticipation participation,
        int phaseCount
    )
    {
        var phases = Enumerable
            .Range(0, phaseCount)
            .Select(index => new Phase(
                new Loop(40 + index, 100 + index),
                40,
                index == phaseCount - 1 ? null : 30,
                200 + index
            ))
            .ToList();

        return new Competition(
            "Competition",
            CompetitionType.Qualification,
            CompetitionRuleset.FEI,
            competitionStart,
            null,
            null,
            null,
            null,
            phases,
            [participation],
            300
        );
    }

    static SetupParticipation CreateSetupParticipation(DateTimeOffset? startTimeOverride)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["John", "Doe"]), null, country, null, 10);
        var horse = new Horse("Horse", null, 11);
        var combination = new Combination(12, athlete, horse, 13);

        return new SetupParticipation(
            isNotRanked: false,
            combination: combination,
            category: ParticipationCategory.Senior,
            startTimeOverride: startTimeOverride,
            maxSpeedOverride: null,
            minSpeedOverride: null,
            id: 14
        );
    }
}
