using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

public sealed class ParticipationArrivedTests
{
    [Fact]
    public void Process_raises_participation_arrived_when_arrive_time_is_first_recorded()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var participation = CreateParticipation(start);

        participation.Process(
            new Snapshot(1, SnapshotType.Arrive, SnapshotMethod.Manual, new Timestamp(start.AddHours(1)))
        );

        Assert.Contains(participation.DequeueDomainEvents(), x => x is ParticipationArrived);
    }

    [Fact]
    public void Process_does_not_raise_participation_arrived_for_later_phase_updates()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var participation = CreateParticipation(start);
        participation.Process(
            new Snapshot(1, SnapshotType.Arrive, SnapshotMethod.Manual, new Timestamp(start.AddHours(1)))
        );
        participation.DequeueDomainEvents();

        participation.Process(
            new Snapshot(1, SnapshotType.Present, SnapshotMethod.Manual, new Timestamp(start.AddHours(1).AddMinutes(10)))
        );

        Assert.DoesNotContain(participation.DequeueDomainEvents(), x => x is ParticipationArrived);
    }

    [Fact]
    public void Process_raises_participation_arrived_when_next_phase_is_selected()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var firstArrive = start.AddHours(1);
        var firstPresent = firstArrive.AddMinutes(10);
        var secondStart = firstPresent.AddMinutes(40);
        var secondArrive = secondStart.AddMinutes(31);
        var participation = CreateParticipation(
            start,
            CreatePhase(start, firstArrive, firstPresent, id: 1),
            CreatePhase(secondStart, id: 2)
        );

        var result = participation.Process(
            new Snapshot(1, SnapshotType.Arrive, SnapshotMethod.Manual, new Timestamp(secondArrive))
        );

        Assert.Equal(SnapshotResultType.Applied, result.Type);
        Assert.Equal(2, participation.Phases.Current.Id);
        Assert.Contains(participation.DequeueDomainEvents(), x => x is ParticipationArrived);
    }

    [Fact]
    public void Update_does_not_raise_participation_arrived_for_non_current_phase()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var firstArrive = start.AddHours(1);
        var firstPresent = firstArrive.AddMinutes(10);
        var secondStart = firstPresent.AddMinutes(40);
        var participation = CreateParticipation(
            start,
            CreatePhase(start, firstArrive, firstPresent, id: 1),
            CreatePhase(secondStart, id: 2)
        );

        participation.Update(
            new PhaseState(2, secondStart, secondStart.AddMinutes(45), null, null)
        );

        Assert.Equal(1, participation.Phases.Current.Id);
        Assert.DoesNotContain(participation.DequeueDomainEvents(), x => x is ParticipationArrived);
    }

    static Participation CreateParticipation(DateTimeOffset start, params Phase[] phases)
    {
        const int number = 1;
        var country = new Country(number, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Athlete", null, country, null, null, number);
        var horse = new Horse("Horse", null, null, number);
        var combination = new Combination(number, athlete, horse, null, "20", null, null, number);
        var phaseList = phases.Length == 0 ? [CreatePhase(start)] : phases;

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection(phaseList),
            null,
            eventId: 1
        );
    }

    static Phase CreatePhase(
        DateTimeOffset start,
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        bool isFinal = false,
        int? id = null
    )
    {
        return new Phase(
            "",
            20,
            40,
            40,
            CompetitionRuleset.Regional,
            isFinal,
            null,
            new Timestamp(start),
            CreateTimestamp(arrive),
            CreateTimestamp(present),
            null,
            false,
            false,
            false,
            id
        );
    }

    static Timestamp? CreateTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp == null ? null : new Timestamp(timestamp.Value);
    }

    sealed class PhaseState : IPhaseState
    {
        public PhaseState(
            int id,
            DateTimeOffset? startTime,
            DateTimeOffset? arriveTime,
            DateTimeOffset? presentTime,
            DateTimeOffset? representTime
        )
        {
            Id = id;
            StartTime = startTime;
            ArriveTime = arriveTime;
            PresentTime = presentTime;
            RepresentTime = representTime;
        }

        public int Id { get; }
        public DateTimeOffset? StartTime { get; }
        public DateTimeOffset? ArriveTime { get; }
        public DateTimeOffset? PresentTime { get; }
        public DateTimeOffset? RepresentTime { get; }
    }
}
