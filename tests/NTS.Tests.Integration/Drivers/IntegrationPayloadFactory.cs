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
    public static EventInformation EventInformation(int eventId, EventSpan? eventSpan = null, string? name = null)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var today = DateTimeOffset.UtcNow.Date;
        return new EventInformation(
            country,
            name ?? "Integration Event",
            "Sofia",
            eventSpan ?? new EventSpan(today, today.AddDays(1)),
            null,
            eventId
        );
    }

    public static Participation ActiveParticipation(
        int eventId,
        int participationNumber,
        int? id = null,
        double? minAverageSpeed = null,
        double? maxAverageSpeed = null,
        DateTimeOffset? startTime = null
    )
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athleteId = id == null ? 101 : id.Value + 100;
        var horseId = id == null ? 201 : id.Value + 200;
        var combinationId = id == null ? 301 : id.Value + 300;
        var phaseId = id == null ? 401 : id.Value + 400;
        var athlete = new Athlete("Integration Rider", "Integration Rider", country, null, null, athleteId);
        var horse = new Horse("Integration Horse", "Integration Horse", null, horseId);
        var combination = new Combination(
            participationNumber,
            athlete,
            horse,
            club: null,
            distance: "40",
            minAverageSpeed: minAverageSpeed,
            maxAverageSpeed: maxAverageSpeed,
            id: combinationId
        );
        var competition = new Competition("CEI 1*", CompetitionRuleset.FEI);
        var phase = new Phase(
            gate: "GATE1/40",
            length: 40,
            maxRecovery: 40,
            rest: null,
            ruleset: CompetitionRuleset.FEI,
            isFinal: true,
            compulsoryThresholdSpan: null,
            startTime: new Timestamp(startTime ?? DateTimeOffset.UtcNow.Date.AddHours(8)),
            arriveTime: null,
            presentTime: null,
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: phaseId
        );

        return new Participation(
            ParticipationCategory.Senior,
            competition,
            combination,
            new PhaseCollection([phase]),
            notQualified: null,
            eventId,
            id: id ?? 501
        );
    }

    public static Participation TwoPhaseParticipation(
        int eventId,
        int participationNumber,
        int id,
        TimeSpan? compulsoryThresholdSpan = null,
        DateTimeOffset? startTime = null
    )
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Integration Rider", "Integration Rider", country, null, null, id + 100);
        var horse = new Horse("Integration Horse", "Integration Horse", null, id + 200);
        var combination = new Combination(
            participationNumber,
            athlete,
            horse,
            club: null,
            distance: "40",
            minAverageSpeed: null,
            maxAverageSpeed: null,
            id: id + 300
        );
        var competition = new Competition("CEI 1*", CompetitionRuleset.FEI);
        var firstPhase = new Phase(
            gate: "GATE1/20",
            length: 20,
            maxRecovery: 40,
            rest: 40,
            ruleset: CompetitionRuleset.FEI,
            isFinal: false,
            compulsoryThresholdSpan: compulsoryThresholdSpan,
            startTime: new Timestamp(startTime ?? DateTimeOffset.UtcNow.Date.AddHours(8)),
            arriveTime: null,
            presentTime: null,
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: id + 400
        );
        var finalPhase = new Phase(
            gate: "GATE2/40",
            length: 20,
            maxRecovery: 40,
            rest: null,
            ruleset: CompetitionRuleset.FEI,
            isFinal: true,
            compulsoryThresholdSpan: null,
            startTime: null,
            arriveTime: null,
            presentTime: null,
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: id + 401
        );

        return new Participation(
            ParticipationCategory.Senior,
            competition,
            combination,
            new PhaseCollection([firstPhase, finalPhase]),
            notQualified: null,
            eventId,
            id
        );
    }

    public static Official Official(int eventId, int? userId, int? id = null)
    {
        return new Official(
            "Integration Official",
            "Integration Official",
            OfficialRole.GroundJury,
            eventId,
            id: id ?? 601,
            userId: userId
        );
    }

    public static Operator Operator(int eventId, int userId, int? id = null)
    {
        return new Operator(eventId, userId, OfficialRole.Steward, id ?? 602);
    }

    public static Ranking Ranking(
        int eventId,
        IEnumerable<Participation> participations,
        int? id = null,
        string? name = null
    )
    {
        var entries = participations.Select(
            (participation, index) => new RankingEntry(participation, index + 1, false, id + index + 1)
        );

        return new Ranking(
            name ?? "Integration Ranking",
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            eventId,
            id ?? 701
        );
    }

    public static Handout Handout(Participation participation, int? id = null)
    {
        return new Handout(participation, id ?? 801);
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
