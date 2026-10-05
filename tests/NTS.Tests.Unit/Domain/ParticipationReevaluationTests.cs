using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// What a Participation does after any accepted change to a Phase's times (#615, ADR-0005), whichever way the change was made
/// (a Snapshot or the Phase form) and whether or not the Phase is the current one. Every case is the same ride: Phase 1
/// starts at 08:00, its Arrival is at 09:00, its Presentation at 09:10, its recovery limit is 40 minutes and its Rest 40
/// minutes, so it is out at 09:50, which is the Start of Phase 2.
/// </summary>
public sealed class ParticipationReevaluationTests
{
    static readonly DateTimeOffset START = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddHours(1);
    static readonly DateTimeOffset PRESENT = ARRIVE.AddMinutes(10);
    static readonly DateTimeOffset OUT = PRESENT.AddMinutes(40);
    static readonly DateTimeOffset RECORDED = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly Guid ACTOR = TestId.Of(9);

    [Fact]
    public void A_change_of_the_presentation_of_a_complete_phase_moves_the_start_of_the_next_phase_in_place()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT, id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );

        participation.Update(FormOf(TestId.Of(1), START, ARRIVE, PRESENT.AddMinutes(5)), ACTOR, RECORDED);

        Assert.Equal(OUT.AddMinutes(5), participation.Phases[1].StartTime!.ToDateTimeOffset());
        Assert.Null(participation.Phases[2].StartTime);
    }

    [Fact]
    public void A_change_that_leaves_the_Out_time_alone_leaves_the_start_of_the_next_phase_where_it_is()
    {
        var editedStart = OUT.AddMinutes(10);
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(editedStart, id: TestId.Of(2))
        );

        participation.Update(FormOf(TestId.Of(1), START, ARRIVE.AddMinutes(2), PRESENT), ACTOR, RECORDED);

        Assert.Equal(editedStart, participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_phase_that_completes_by_a_change_starts_the_next_one_and_is_announced_once()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );

        participation.Update(FormOf(TestId.Of(1), START, ARRIVE, PRESENT), ACTOR, RECORDED);

        Assert.Equal(OUT, participation.Phases[1].StartTime!.ToDateTimeOffset());
        var completed = Assert.Single(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(TestId.Of(1), completed.PhaseId);
    }

    [Fact]
    public void A_change_to_a_complete_phase_that_is_not_the_current_one_announces_it_again_and_leaves_the_current_phase_alone()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT, OUT.AddMinutes(30), OUT.AddMinutes(40), id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );
        Assert.Equal(TestId.Of(2), participation.Phases.Current.Id);

        participation.Update(FormOf(TestId.Of(1), START, ARRIVE, PRESENT.AddMinutes(5)), ACTOR, RECORDED);

        var completed = Assert.Single(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(TestId.Of(1), completed.PhaseId);
        Assert.Equal(TestId.Of(2), participation.Phases.Current.Id);
        Assert.Equal(OUT.AddMinutes(5), participation.Phases[1].StartTime!.ToDateTimeOffset());
        Assert.Null(participation.Phases[2].StartTime);
    }

    [Fact]
    public void Restoring_a_participation_whose_current_phase_is_complete_starts_the_next_phase_and_announces_the_phase()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT.AddMinutes(10), id: TestId.Of(2))
        );
        participation.Withdraw();
        participation.DequeueDomainEvents();

        participation.Restore();

        Assert.Null(participation.Eliminated);
        Assert.Equal(OUT, participation.Phases[1].StartTime!.ToDateTimeOffset());
        var completed = Assert.Single(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(TestId.Of(1), completed.PhaseId);
    }

    [Fact]
    public void Restoring_a_participation_whose_current_phase_is_not_complete_announces_nothing()
    {
        var participation = Ride(CreatePhase(START, ARRIVE, id: TestId.Of(1)), CreatePhase(null, id: TestId.Of(2)));
        participation.Withdraw();
        participation.DequeueDomainEvents();

        participation.Restore();

        Assert.Null(participation.Eliminated);
        Assert.Empty(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Null(participation.Phases[1].StartTime);
    }

    [Fact]
    public void A_change_that_leaves_the_phase_incomplete_announces_nothing_and_leaves_the_next_start()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT, id: TestId.Of(2))
        );

        participation.Update(FormOf(TestId.Of(1), START, ARRIVE, null), ACTOR, RECORDED);

        Assert.Empty(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(OUT, participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_change_that_puts_the_recovery_over_the_limit_eliminates_for_time_and_announces_nothing()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT, id: TestId.Of(2))
        );

        participation.Update(FormOf(TestId.Of(1), START, ARRIVE, ARRIVE.AddMinutes(50)), ACTOR, RECORDED);

        var eliminated = Assert.IsType<FailedToQualify>(participation.Eliminated);
        Assert.Equal([FailToQualifyCode.OT], eliminated.FtqCodes);
        Assert.Empty(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(OUT, participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_change_that_brings_the_recovery_within_the_limit_restores_a_participation_eliminated_for_time_and_announces_the_phase_once()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Present(ARRIVE.AddMinutes(50)));
        Assert.IsType<FailedToQualify>(participation.Eliminated);
        participation.DequeueDomainEvents();

        participation.Update(FormOf(TestId.Of(1), START, ARRIVE, PRESENT), ACTOR, RECORDED);

        Assert.Null(participation.Eliminated);
        var completed = Assert.Single(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(TestId.Of(1), completed.PhaseId);
        Assert.Equal(OUT, participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_change_leaves_an_elimination_made_by_hand_as_it_is_when_the_recovery_is_within_the_limit()
    {
        var participation = Ride(CreatePhase(START, ARRIVE, id: TestId.Of(1)));
        participation.Withdraw();

        participation.Update(FormOf(TestId.Of(1), START, ARRIVE, PRESENT), ACTOR, RECORDED);

        Assert.IsType<Withdrawn>(participation.Eliminated);
    }

    [Fact]
    public void A_Snapshot_that_leaves_the_Out_time_alone_leaves_the_start_of_the_next_phase_where_it_is()
    {
        var editedStart = OUT.AddMinutes(10);
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(editedStart, id: TestId.Of(2))
        );

        var recorded = Process(participation, Present(PRESENT));

        Assert.Equal(TimeEventOutcome.Accepted, recorded.Outcome);
        Assert.Equal(editedStart, participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_Snapshot_that_replaces_the_presentation_of_a_complete_phase_moves_the_next_start_in_place()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT, id: TestId.Of(2))
        );
        var later = PRESENT.AddMinutes(10);

        Process(participation, Present(later));

        Assert.Equal(later.AddMinutes(40), participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    static TimeEvent Process(Participation participation, Snapshot snapshot)
    {
        return participation.Process(snapshot, ACTOR, RECORDED);
    }

    static Snapshot Present(DateTimeOffset time)
    {
        return new Snapshot(1, SnapshotType.Present, SnapshotMethod.Manual, new Timestamp(time));
    }

    static Form FormOf(
        Guid id,
        DateTimeOffset? start,
        DateTimeOffset? arrive,
        DateTimeOffset? present,
        DateTimeOffset? represent = null
    )
    {
        return new Form(id, start, arrive, present, represent);
    }

    static Participation Ride(params Phase[] phases)
    {
        const int number = 1;
        var country = new Country(TestId.Of(number), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Athlete", null, country, null, null, TestId.Of(number));
        var horse = new Horse("Horse", null, null, TestId.Of(number));
        var combination = new Combination(number, athlete, horse, null, "20", null, null, TestId.Of(number));

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection(phases),
            null,
            eventId: TestId.Of(1)
        );
    }

    static Phase CreatePhase(
        DateTimeOffset? start,
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        DateTimeOffset? represent = null,
        bool isFinal = false,
        Guid? id = null
    )
    {
        return new Phase(
            "",
            20,
            40,
            isFinal ? null : 40,
            CompetitionRuleset.Regional,
            isFinal,
            null,
            Timestamp.Create(start),
            Timestamp.Create(arrive),
            Timestamp.Create(present),
            Timestamp.Create(represent),
            false,
            false,
            false,
            id
        );
    }

    sealed class Form : IPhaseState
    {
        public Form(
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
