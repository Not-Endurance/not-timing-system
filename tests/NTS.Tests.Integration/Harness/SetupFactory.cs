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

namespace NTS.Tests.Integration.Harness;

/// <summary>
/// A Setup with everything in it, as a Console Operator would have configured it: a competition of three phases over two
/// loops, a combination that rides in it, an Official and an Operator who are named by an email. It is what a change of a
/// Setup is shown to carry whole, so every kind of member the Setup has is in it once.
/// </summary>
internal static class SetupFactory
{
    /// <param name="startNumberInTwoCompetitions">
    /// Whether the start number of the Combination is held by a second Competition whose Phases differ from the first one's,
    /// which an Event cannot start with until the Participation is taken out of one of them.
    /// </param>
    /// <param name="eachHasAnother">
    /// Whether each of those Competitions has a Participation of its own besides the one they both hold; when it has not, taking
    /// that one out leaves it empty, which an Event cannot start with either.
    /// </param>
    public static SetupConfigureEvent Full(
        Guid id,
        string name = "Full Setup",
        DateTimeOffset? start = null,
        bool startNumberInTwoCompetitions = false,
        bool eachHasAnother = true
    )
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
        var combinations = new List<SetupCombination> { combination };
        var firstParticipations = new List<SetupParticipation> { participation };
        var secondParticipations = new List<SetupParticipation>();
        if (startNumberInTwoCompetitions && eachHasAnother)
        {
            // each Competition has a Participation of its own besides the one they both hold, so that it is not left empty when that is taken out of it
            foreach (
                var (number, own, rides) in new[] { (102, 21, firstParticipations), (103, 22, secondParticipations) }
            )
            {
                var only = new SetupCombination(
                    number,
                    new SetupAthlete(
                        $"Атлет {number}",
                        $"Athlete {number}",
                        $"1001{number}",
                        country,
                        null,
                        TestId.Of(own)
                    ),
                    new SetupHorse($"Кон {number}", $"Horse {number}", $"103AB{number}", TestId.Of(own + 10)),
                    TestId.Of(own + 20)
                );
                combinations.Add(only);
                rides.Add(
                    new SetupParticipation(
                        isNotRanked: false,
                        combination: only,
                        category: ParticipationCategory.Senior,
                        startTimeOverride: null,
                        maxSpeedOverride: null,
                        minSpeedOverride: null,
                        id: TestId.Of(own + 40)
                    )
                );
            }
        }

        var competition = new SetupCompetition(
            "CEI 1*",
            CompetitionRuleset.FEI,
            start ?? new DateTimeOffset(2030, 5, 21, 8, 0, 0, TimeSpan.Zero),
            compulsoryThresholdSpan: TimeSpan.FromMinutes(10),
            minSpeedRestriction: 12.0,
            maxSpeedRestriction: 25.0,
            feiEventId: "FEI-EVENT",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMPETITION",
            feiRule: "Rule",
            feiScheduleNumber: "1",
            phases,
            firstParticipations,
            id: TestId.Of(70)
        );
        var competitions = new List<SetupCompetition> { competition };
        if (startNumberInTwoCompetitions)
        {
            competitions.Add(
                new SetupCompetition(
                    "CEI 2*",
                    CompetitionRuleset.FEI,
                    start ?? new DateTimeOffset(2030, 5, 21, 8, 0, 0, TimeSpan.Zero),
                    compulsoryThresholdSpan: TimeSpan.FromMinutes(10),
                    minSpeedRestriction: 12.0,
                    maxSpeedRestriction: 25.0,
                    feiEventId: "FEI-EVENT",
                    feiEventCode: "CEI2",
                    feiCompetitionId: "FEI-COMPETITION-2",
                    feiRule: "Rule",
                    feiScheduleNumber: "2",
                    [
                        new SetupPhase(loop20, recovery: 15, rest: 40, id: TestId.Of(53)),
                        new SetupPhase(loop10, recovery: 20, rest: null, id: TestId.Of(54)),
                    ],
                    [
                        new SetupParticipation(
                            isNotRanked: false,
                            combination: combination,
                            category: ParticipationCategory.Senior,
                            startTimeOverride: null,
                            maxSpeedOverride: null,
                            minSpeedOverride: null,
                            id: TestId.Of(61)
                        ),
                        .. secondParticipations,
                    ],
                    id: TestId.Of(71)
                )
            );
        }

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
            competitions,
            [official],
            [loop20, loop10],
            combinations,
            id,
            [accessOperator]
        );
    }
}
