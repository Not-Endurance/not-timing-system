using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupClub = NTS.Domain.Setup.Aggregates.Club;
using SetupCombination = NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupLoop = NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop;
using SetupOfficial = NTS.Domain.Setup.Aggregates.ConfigureEvents.Official;
using SetupOperator = NTS.Domain.Setup.Aggregates.ConfigureEvents.Operator;
using SetupParticipation = NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation;
using SetupPhase = NTS.Domain.Setup.Aggregates.ConfigureEvents.Phase;
using SetupUser = NTS.Domain.Setup.Aggregates.User;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// A Setup with everything in it, as a Main Operator would have configured it: a competition of three phases over two
/// loops, a combination that rides in it, an Official and an Operator who are named by an email. It is what a change of a
/// Setup is shown to carry whole, so every kind of member the Setup has is in it once.
/// </summary>
internal static class SetupFactory
{
    public static SetupConfigureEvent Full(Guid id, string name = "Full Setup")
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var loop20 = new SetupLoop(20, TestId.Of(10));
        var loop10 = new SetupLoop(10, TestId.Of(11));
        var athlete = new SetupAthlete(
            "Иван Петров",
            "Ivan Petrov",
            "10012345",
            country,
            new SetupClub("Sofia Riders", TestId.Of(2)),
            TestId.Of(20)
        );
        var horse = new SetupHorse("Бърза", "Barza", "103AB45", TestId.Of(30));
        var combination = new SetupCombination(101, athlete, horse, TestId.Of(40));
        var phases = new[]
        {
            new SetupPhase(loop20, recovery: 15, rest: 40, id: TestId.Of(50)),
            new SetupPhase(loop20, recovery: 15, rest: 40, id: TestId.Of(51), isCompulsoryInspectionRequired: true),
            new SetupPhase(loop10, recovery: 20, rest: null, id: TestId.Of(52)),
        };
        var participation = new SetupParticipation(
            isNotRanked: false,
            combination: combination,
            category: ParticipationCategory.Senior,
            startTimeOverride: null,
            maxSpeedOverride: null,
            minSpeedOverride: 12.5,
            id: TestId.Of(60)
        );
        var competition = new SetupCompetition(
            "CEI 1*",
            CompetitionRuleset.FEI,
            new DateTimeOffset(2030, 5, 21, 8, 0, 0, TimeSpan.Zero),
            compulsoryThresholdSpan: TimeSpan.FromMinutes(10),
            minSpeedRestriction: 12.0,
            maxSpeedRestriction: 25.0,
            feiEventId: "FEI-EVENT",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMPETITION",
            feiRule: "Rule",
            feiScheduleNumber: "1",
            phases,
            [participation],
            id: TestId.Of(70)
        );
        var official = new SetupOfficial(
            "Георги Иванов",
            "Georgi Ivanov",
            OfficialRole.Steward,
            TestId.Of(80),
            new SetupUser("georgi.ivanov@example.test", "Georgi Ivanov", [], TestId.Of(81))
        );
        var accessOperator = new SetupOperator(
            new SetupUser("operator.one@example.test", "Operator One", [], TestId.Of(91)),
            TestId.Of(90)
        );

        return new SetupConfigureEvent(
            name,
            "Sofia",
            country,
            "FEI42",
            [competition],
            [official],
            [loop20, loop10],
            [combination],
            id,
            [accessOperator]
        );
    }
}
