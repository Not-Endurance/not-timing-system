using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using Competition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;

namespace NTS.Tests.Integration.Drivers;

internal static class IntegrationPayloadFactory
{
    public static EventInformation EventInformation(Guid eventId, EventSpan? eventSpan = null, string? name = null)
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
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
        Guid eventId,
        int participationNumber,
        Guid? id = null,
        double? minAverageSpeed = null,
        double? maxAverageSpeed = null,
        DateTimeOffset? startTime = null
    )
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var athleteId = id == null ? TestId.Of(101) : Offset(id.Value, 100);
        var horseId = id == null ? TestId.Of(201) : Offset(id.Value, 200);
        var combinationId = id == null ? TestId.Of(301) : Offset(id.Value, 300);
        var phaseId = id == null ? TestId.Of(401) : Offset(id.Value, 400);
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
            id: id ?? TestId.Of(501)
        );
    }

    public static Participation TwoPhaseParticipation(
        Guid eventId,
        int participationNumber,
        Guid id,
        TimeSpan? compulsoryThresholdSpan = null,
        DateTimeOffset? startTime = null
    )
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Integration Rider", "Integration Rider", country, null, null, Offset(id, 100));
        var horse = new Horse("Integration Horse", "Integration Horse", null, Offset(id, 200));
        var combination = new Combination(
            participationNumber,
            athlete,
            horse,
            club: null,
            distance: "40",
            minAverageSpeed: null,
            maxAverageSpeed: null,
            id: Offset(id, 300)
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
            id: Offset(id, 400)
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
            id: Offset(id, 401)
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

    public static Official Official(Guid eventId, Guid? userId, Guid? id = null)
    {
        return new Official(
            "Integration Official",
            "Integration Official",
            OfficialRole.GroundJury,
            eventId,
            id: id ?? TestId.Of(601),
            userId: userId
        );
    }

    public static Operator Operator(Guid eventId, Guid userId, Guid? id = null)
    {
        return new Operator(eventId, userId, OfficialRole.Steward, id ?? TestId.Of(602));
    }

    public static Ranking Ranking(
        Guid eventId,
        IEnumerable<Participation> participations,
        Guid? id = null,
        string? name = null
    )
    {
        var entries = participations.Select(
            (participation, index) => new RankingEntry(participation.Id, false, index + 1)
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
            id ?? TestId.Of(701)
        );
    }

    public static Handout Handout(Participation participation, Guid? id = null)
    {
        return new Handout(participation.EventId, participation.Id, id ?? TestId.Of(801));
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

    /// <summary>
    /// A stable id derived from another: <c>Offset(TestId.Of(5), 100)</c> is <c>TestId.Of(105)</c>.
    /// </summary>
    static Guid Offset(Guid id, int offset)
    {
        var bytes = id.ToByteArray();
        BitConverter.GetBytes(BitConverter.ToInt32(bytes, 0) + offset).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}
