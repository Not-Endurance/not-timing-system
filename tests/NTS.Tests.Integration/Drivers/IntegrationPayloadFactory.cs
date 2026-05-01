using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using Competition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;

namespace NTS.Tests.Integration.Drivers;

internal static class IntegrationPayloadFactory
{
    public static EnduranceEvent EnduranceEvent(int eventId)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            country,
            "Integration Event",
            "Sofia",
            new EventSpan(DateTimeOffset.UtcNow.Date, DateTimeOffset.UtcNow.Date.AddDays(1)),
            null,
            null,
            null,
            eventId
        );
    }

    public static Participation ActiveParticipation(int eventId, int participationNumber)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["Integration", "Rider"]), country, null, null, 101);
        var horse = new Horse("Integration Horse", null, 201);
        var combination = new Combination(
            participationNumber,
            athlete,
            horse,
            club: null,
            distance: "40",
            minAverageSpeed: null,
            maxAverageSpeed: null,
            id: 301
        );
        var competition = new Competition("CEI 1*", CompetitionRuleset.FEI, CompetitionType.Qualification);
        var phase = new Phase(
            gate: "GATE1/40",
            length: 40,
            maxRecovery: 40,
            rest: null,
            ruleset: CompetitionRuleset.FEI,
            isFinal: true,
            compulsoryThresholdSpan: null,
            startTime: new Timestamp(DateTimeOffset.UtcNow.Date.AddHours(8)),
            arriveTime: null,
            presentTime: null,
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: 401
        );

        return new Participation(
            ParticipationCategory.Senior,
            competition,
            combination,
            new PhaseCollection([phase]),
            notQualified: null,
            eventId,
            id: 501
        );
    }

    public static Official Official(int eventId, int userId)
    {
        return new Official(
            new Person(["Integration", "Official"]),
            OfficialRole.GroundJury,
            eventId,
            id: 601,
            userId: userId
        );
    }

    public static Snapshot AutomaticSnapshot(int participationNumber, DateTimeOffset timestamp)
    {
        return new Snapshot(
            participationNumber,
            SnapshotType.Automatic,
            SnapshotMethod.Manual,
            new Timestamp(timestamp)
        );
    }

    public static PhaseCompleted PhaseCompleted(int eventId, int participationNumber)
    {
        var participation = ActiveParticipation(eventId, participationNumber);
        participation.Process(AutomaticSnapshot(participationNumber, DateTimeOffset.UtcNow.Date.AddHours(10)));
        participation.Process(
            AutomaticSnapshot(participationNumber, DateTimeOffset.UtcNow.Date.AddHours(10).AddMinutes(5))
        );

        return new PhaseCompleted(participation);
    }
}
