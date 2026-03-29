using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Judge.Tests.Domain;

public class ParticipationRestoreTests
{
    [Fact]
    public void Restore_WhenCurrentPhaseIsComplete_StartsNextPhaseAndRaisesPhaseCompleted()
    {
        var now = DateTimeOffset.Now;
        var phase1 = CreatePhase(
            length: 40,
            maxRecovery: 40,
            rest: 30,
            isFinal: false,
            startTime: now.AddMinutes(-70),
            arriveTime: now.AddMinutes(-20),
            presentTime: now.AddMinutes(-10),
            id: 5001
        );
        var phase2 = CreatePhase(
            length: 40,
            maxRecovery: 40,
            rest: null,
            isFinal: true,
            startTime: null,
            arriveTime: null,
            presentTime: null,
            id: 5002
        );
        var participation = CreateParticipation(
            71,
            new PhaseCollection([phase1, phase2]),
            new FailedToQualify([FailToQualifyCode.SP])
        );

        participation.Restore();

        Assert.Null(participation.Eliminated);
        Assert.Equal(phase1.GetOutTime(), participation.Phases[1].StartTime);

        var events = participation.DequeueDomainEvents();
        Assert.Collection(
            events,
            domainEvent => Assert.IsType<ParticipationRestored>(domainEvent),
            domainEvent => Assert.IsType<PhaseCompleted>(domainEvent)
        );
    }

    static Participation CreateParticipation(int number, PhaseCollection phases, Eliminated? eliminated)
    {
        var country = new Country(1001, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["Rider", "Restore"]), country, null, null, 2001);
        var horse = new Horse("HorseRestore", null, 3001);
        var combination = new Combination(number, athlete, horse, null, "80", 12, null, 4001);

        return new Participation(
            category: ParticipationCategory.Senior,
            competition: new Competition("Competition", CompetitionRuleset.FEI, CompetitionType.Star),
            combination: combination,
            phases: phases,
            notQualified: eliminated,
            eventId: 6001,
            id: 7001
        );
    }

    static Phase CreatePhase(
        double length,
        int maxRecovery,
        int? rest,
        bool isFinal,
        DateTimeOffset? startTime,
        DateTimeOffset? arriveTime,
        DateTimeOffset? presentTime,
        int id
    )
    {
        return new Phase(
            gate: "",
            length: length,
            maxRecovery: maxRecovery,
            rest: rest,
            ruleset: CompetitionRuleset.FEI,
            isFinal: isFinal,
            compulsoryThresholdSpan: null,
            startTime: Timestamp.Create(startTime),
            arriveTime: Timestamp.Create(arriveTime),
            presentTime: Timestamp.Create(presentTime),
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: id
        );
    }
}
