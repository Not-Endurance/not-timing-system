using Not.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

public sealed class ParticipationProcessTests
{
    static readonly DateTimeOffset RECORDED = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly Guid ACTOR = TestId.Of(9);

    [Fact]
    public void Process_raises_phase_completed_with_the_ids_and_not_final_when_a_phase_completes_and_another_follows()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var arrive = start.AddHours(1);
        var participation = CreateParticipation(
            start,
            CreatePhase(start, arrive, id: TestId.Of(1)),
            CreatePhase(arrive.AddMinutes(50), isFinal: true, id: TestId.Of(2))
        );

        Process(
            participation,
            new Snapshot(1, SnapshotType.Present, SnapshotMethod.Manual, new Timestamp(arrive.AddMinutes(10)))
        );

        var completed = Assert.Single(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(participation.Id, completed.ParticipationId);
        Assert.Equal(1, completed.Number);
        Assert.Equal(TestId.Of(1), completed.PhaseId);
        Assert.False(completed.IsFinal);
    }

    [Fact]
    public void Process_raises_phase_completed_marked_final_when_the_final_phase_completes()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var arrive = start.AddHours(1);
        var participation = CreateParticipation(start, CreatePhase(start, arrive, isFinal: true, id: TestId.Of(1)));

        Process(
            participation,
            new Snapshot(1, SnapshotType.Present, SnapshotMethod.Manual, new Timestamp(arrive.AddMinutes(10)))
        );

        var completed = Assert.Single(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(TestId.Of(1), completed.PhaseId);
        Assert.True(completed.IsFinal);
    }

    [Fact]
    public void Restore_raises_phase_completed_when_the_restored_Participation_had_completed_its_phase()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var arrive = start.AddHours(1);
        var participation = CreateParticipation(
            start,
            CreatePhase(start, arrive, arrive.AddMinutes(10), isFinal: true, id: TestId.Of(1))
        );
        participation.Withdraw();
        participation.DequeueDomainEvents();

        participation.Restore();

        var completed = Assert.Single(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(TestId.Of(1), completed.PhaseId);
        Assert.True(completed.IsFinal);
    }

    [Fact]
    public void Process_eliminates_for_time_when_the_recovery_of_the_phase_the_snapshot_was_placed_in_is_over_the_limit()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var firstArrive = start.AddHours(1);
        var firstPresent = firstArrive.AddMinutes(10);
        var secondStart = firstPresent.AddMinutes(40);
        var secondArrive = secondStart.AddMinutes(10);
        var participation = CreateParticipation(
            start,
            CreatePhase(start, firstArrive, firstPresent, id: TestId.Of(1)),
            CreatePhase(secondStart, secondArrive, id: TestId.Of(2))
        );

        var result = Process(
            participation,
            new Snapshot(1, SnapshotType.Present, SnapshotMethod.Manual, new Timestamp(secondArrive.AddMinutes(50)))
        );

        Assert.Equal(TimeEventOutcome.Accepted, result.Outcome);
        Assert.IsType<FailedToQualify>(participation.Eliminated);
        Assert.Equal(TestId.Of(2), participation.Phases.Current.Id);
    }

    [Fact]
    public void Process_leaves_an_elimination_as_it_is_when_the_snapshot_is_applied_to_a_Participation_that_was_eliminated()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var arrive = start.AddHours(1);
        var participation = CreateParticipation(start, CreatePhase(start, arrive, isFinal: true, id: TestId.Of(1)));
        participation.Withdraw();

        var result = Process(
            participation,
            new Snapshot(1, SnapshotType.Present, SnapshotMethod.Manual, new Timestamp(arrive.AddHours(2)))
        );

        Assert.Equal(TimeEventOutcome.Accepted, result.Outcome);
        Assert.IsType<Withdrawn>(participation.Eliminated);
    }

    [Fact]
    public void Process_refuses_a_snapshot_of_a_type_that_does_not_exist_such_as_the_untyped_one_that_was_removed()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var participation = CreateParticipation(start);
        const SnapshotType removedUntypedSnapshot = (SnapshotType)4;
        var untyped = new Snapshot(1, removedUntypedSnapshot, SnapshotMethod.Manual, new Timestamp(start.AddHours(1)));

        Assert.Throws<GuardException>(() => Process(participation, untyped));

        Assert.Null(participation.Phases[0].ArriveTime);
    }

    [Fact]
    public void Process_raises_no_phase_completed_while_the_phase_is_not_complete()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var participation = CreateParticipation(start);

        Process(
            participation,
            new Snapshot(1, SnapshotType.Arrive, SnapshotMethod.Manual, new Timestamp(start.AddHours(1)))
        );

        Assert.DoesNotContain(participation.DequeueDomainEvents(), x => x is PhaseCompleted);
    }

    [Fact]
    public void Process_places_an_arrive_stamped_after_the_Start_of_the_next_phase_in_it_and_selects_it()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var firstArrive = start.AddHours(1);
        var firstPresent = firstArrive.AddMinutes(10);
        var secondStart = firstPresent.AddMinutes(40);
        var secondArrive = secondStart.AddMinutes(5);
        var participation = CreateParticipation(
            start,
            CreatePhase(start, firstArrive, firstPresent, id: TestId.Of(1)),
            CreatePhase(secondStart, id: TestId.Of(2))
        );

        var result = Process(
            participation,
            new Snapshot(1, SnapshotType.Arrive, SnapshotMethod.Manual, new Timestamp(secondArrive))
        );

        Assert.Equal(TimeEventOutcome.Accepted, result.Outcome);
        Assert.Equal(TestId.Of(2), participation.Phases.Current.Id);
    }

    [Fact]
    public void Update_leaves_the_current_phase_alone_when_it_changes_a_phase_that_is_not_current()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var firstArrive = start.AddHours(1);
        var firstPresent = firstArrive.AddMinutes(10);
        var secondStart = firstPresent.AddMinutes(40);
        var participation = CreateParticipation(
            start,
            CreatePhase(start, firstArrive, firstPresent, id: TestId.Of(1)),
            CreatePhase(secondStart, id: TestId.Of(2))
        );

        participation.Update(
            new PhaseState(TestId.Of(2), secondStart, secondStart.AddMinutes(45), null, null),
            ACTOR,
            RECORDED
        );

        Assert.Equal(TestId.Of(1), participation.Phases.Current.Id);
    }

    static TimeEvent Process(Participation participation, Snapshot snapshot)
    {
        return participation.Process(snapshot, ACTOR, RECORDED);
    }

    static Participation CreateParticipation(DateTimeOffset start, params Phase[] phases)
    {
        const int number = 1;
        var country = new Country(TestId.Of(number), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Athlete", null, country, null, null, TestId.Of(number));
        var horse = new Horse("Horse", null, null, TestId.Of(number));
        var combination = new Combination(number, athlete, horse, null, "20", null, null, TestId.Of(number));
        var phaseList = phases.Length == 0 ? [CreatePhase(start)] : phases;

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection(phaseList),
            null,
            eventId: TestId.Of(1)
        );
    }

    static Phase CreatePhase(
        DateTimeOffset start,
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        bool isFinal = false,
        Guid? id = null
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
            Guid id,
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

        public Guid Id { get; }
        public DateTimeOffset? StartTime { get; }
        public DateTimeOffset? ArriveTime { get; }
        public DateTimeOffset? PresentTime { get; }
        public DateTimeOffset? RepresentTime { get; }
    }
}
