using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// What processing a Snapshot leaves on its Phase (#615, ADR-0005): a time event with an outcome, whether the time was
/// accepted or not. Nothing is thrown for a time that cannot be accepted: it is recorded as rejected and the times stay.
/// The Phase starts at 08:00 and its Rest is 40 minutes, so a Snapshot is in order when it follows the Start, an Arrival
/// follows the Start (or is at it), a Presentation follows the Arrival and a Representation follows the Presentation.
/// </summary>
public sealed class ParticipationTimeEventsTests
{
    static readonly DateTimeOffset START = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddHours(1);
    static readonly DateTimeOffset PRESENT = ARRIVE.AddMinutes(10);
    static readonly DateTimeOffset RECORDED = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly Guid ACTOR = TestId.Of(9);

    [Fact]
    public void An_accepted_arrive_is_recorded_with_its_time_method_recording_time_and_actor()
    {
        var participation = Ride(CreatePhase());

        var recorded = participation.Process(Arrive(ARRIVE, SnapshotMethod.RFID), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.Accepted, recorded.Outcome);
        var arrived = Assert.IsType<Arrived>(Assert.Single(participation.Phases[0].Events));
        Assert.Same(recorded, arrived);
        Assert.Equal(ARRIVE, arrived.Time!.ToDateTimeOffset());
        Assert.Equal(SnapshotMethod.RFID, arrived.Method);
        Assert.Equal(RECORDED, arrived.RecordedAt);
        Assert.Equal(ACTOR, arrived.ActorId);
        Assert.NotEqual(Guid.Empty, arrived.Id);
        Assert.Equal(ARRIVE, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_accepted_presentation_is_recorded_with_its_method_recording_time_and_actor()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));

        var recorded = participation.Process(Present(PRESENT, SnapshotMethod.RFID), ACTOR, RECORDED);

        var presented = Assert.IsType<Presented>(recorded);
        Assert.Equal(TimeEventOutcome.Accepted, presented.Outcome);
        Assert.Equal(SnapshotMethod.RFID, presented.Method);
        Assert.Equal(RECORDED, presented.RecordedAt);
        Assert.Equal(ACTOR, presented.ActorId);
        Assert.Equal(PRESENT, presented.Time.ToDateTimeOffset());
    }

    [Fact]
    public void A_rejected_presentation_is_recorded_with_its_method_too()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));

        var recorded = participation.Process(Present(ARRIVE.AddMinutes(-5), SnapshotMethod.RFID), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, recorded.Outcome);
        Assert.Equal(SnapshotMethod.RFID, recorded.Method);
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(15, true)]
    [InlineData(20, true)]
    public void A_presentation_that_takes_the_recovery_to_the_compulsory_threshold_makes_the_required_inspection_compulsory(
        int recoveryMinutes,
        bool isCompulsory
    )
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE, compulsoryThreshold: TimeSpan.FromMinutes(15)));

        participation.Process(Present(ARRIVE.AddMinutes(recoveryMinutes)), ACTOR, RECORDED);

        Assert.Equal(isCompulsory, participation.Phases[0].IsRequiredInspectionCompulsory);
    }

    [Fact]
    public void A_second_arrive_is_recorded_as_a_rejected_duplicate_and_the_time_stays()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));

        var recorded = participation.Process(Arrive(ARRIVE.AddMinutes(5)), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedDuplicateArrive, recorded.Outcome);
        Assert.Equal(2, participation.Phases[0].Events.Count);
        Assert.Same(recorded, participation.Phases[0].Events[1]);
        Assert.Equal(ARRIVE, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_presentation_is_recorded_unmarked_and_replaces_the_one_before_it_while_no_representation_is_requested()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE, present: PRESENT));
        var later = PRESENT.AddMinutes(5);

        var recorded = participation.Process(Present(later), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.Accepted, recorded.Outcome);
        var presented = Assert.IsType<Presented>(recorded);
        Assert.False(presented.IsRepresent);
        Assert.Equal(2, participation.Phases[0].Events.OfType<Presented>().Count());
        Assert.Equal(later, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_presentation_after_a_representation_was_requested_is_recorded_marked_and_feeds_the_representation()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE, present: PRESENT, isRepresentRequested: true));
        var represent = PRESENT.AddMinutes(30);

        var recorded = participation.Process(Present(represent), ACTOR, RECORDED);

        var presented = Assert.IsType<Presented>(recorded);
        Assert.Equal(TimeEventOutcome.Accepted, presented.Outcome);
        Assert.True(presented.IsRepresent);
        Assert.Equal(represent, participation.Phases[0].RepresentTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_presentation_when_the_representation_that_was_requested_exists_too_is_a_rejected_duplicate_present()
    {
        var represent = PRESENT.AddMinutes(30);
        var participation = Ride(
            CreatePhase(arrive: ARRIVE, present: PRESENT, represent: represent, isRepresentRequested: true)
        );

        var recorded = participation.Process(Present(represent.AddMinutes(5)), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedDuplicatePresent, recorded.Outcome);
        Assert.Equal(represent, participation.Phases[0].RepresentTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_arrive_stamped_before_the_Start_is_recorded_as_a_rejected_invalid_time_and_nothing_is_thrown()
    {
        var participation = Ride(CreatePhase());

        var recorded = participation.Process(Arrive(START.AddMinutes(-1)), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, recorded.Outcome);
        Assert.Null(participation.Phases[0].ArriveTime);
        Assert.Same(recorded, Assert.Single(participation.Phases[0].Events));
    }

    [Fact]
    public void An_arrive_stamped_at_the_Start_is_in_order()
    {
        var participation = Ride(CreatePhase());

        var recorded = participation.Process(Arrive(START), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.Accepted, recorded.Outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_presentation_at_or_before_the_arrival_is_a_rejected_invalid_time(int minutesAfterTheArrival)
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));

        var recorded = participation.Process(Present(ARRIVE.AddMinutes(minutesAfterTheArrival)), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, recorded.Outcome);
        Assert.Null(participation.Phases[0].PresentTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_representation_at_or_before_the_presentation_is_a_rejected_invalid_time(
        int minutesAfterThePresentation
    )
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE, present: PRESENT, isRepresentRequested: true));

        var recorded = participation.Process(Present(PRESENT.AddMinutes(minutesAfterThePresentation)), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, recorded.Outcome);
        Assert.True(Assert.IsType<Presented>(recorded).IsRepresent);
        Assert.Null(participation.Phases[0].RepresentTime);
    }

    [Fact]
    public void A_presentation_without_an_arrival_is_in_order_as_long_as_it_follows_the_Start()
    {
        var participation = Ride(CreatePhase());

        var recorded = participation.Process(Present(PRESENT), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.Accepted, recorded.Outcome);
        Assert.Null(participation.Phases[0].ArriveTime);
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_presentation_without_an_arrival_stamped_before_the_Start_is_a_rejected_invalid_time()
    {
        var participation = Ride(CreatePhase());

        var recorded = participation.Process(Present(START.AddMinutes(-1)), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, recorded.Outcome);
    }

    [Fact]
    public void A_presentation_that_reaches_a_representation_the_phase_has_is_a_rejected_invalid_time()
    {
        var represent = PRESENT.AddMinutes(30);
        var reaching = Ride(CreatePhase(arrive: ARRIVE, present: PRESENT, represent: represent));
        var before = Ride(CreatePhase(arrive: ARRIVE, present: PRESENT, represent: represent));

        var rejected = reaching.Process(Present(represent), ACTOR, RECORDED);
        var accepted = before.Process(Present(represent.AddMinutes(-1)), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, rejected.Outcome);
        Assert.Equal(PRESENT, reaching.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Equal(TimeEventOutcome.Accepted, accepted.Outcome);
        Assert.Equal(represent.AddMinutes(-1), before.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_arrive_stamped_at_the_time_of_the_presentation_is_a_rejected_invalid_time()
    {
        var participation = Ride(CreatePhase(present: PRESENT));

        var recorded = participation.Process(Arrive(PRESENT), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, recorded.Outcome);
        Assert.Null(participation.Phases[0].ArriveTime);
    }

    [Fact]
    public void An_arrive_is_checked_against_the_representation_when_the_phase_has_no_presentation()
    {
        var represent = PRESENT.AddMinutes(30);
        var after = Ride(CreatePhase(represent: represent));
        var before = Ride(CreatePhase(represent: represent));

        var rejected = after.Process(Arrive(represent.AddMinutes(1)), ACTOR, RECORDED);
        var accepted = before.Process(Arrive(represent.AddMinutes(-1)), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, rejected.Outcome);
        Assert.Equal(TimeEventOutcome.Accepted, accepted.Outcome);
    }

    [Fact]
    public void A_late_arrive_stamped_after_the_presentation_is_a_rejected_invalid_time_and_an_earlier_one_is_accepted()
    {
        var late = Ride(CreatePhase(present: PRESENT));
        var early = Ride(CreatePhase(present: PRESENT));

        var rejected = late.Process(Arrive(PRESENT.AddMinutes(1)), ACTOR, RECORDED);
        var accepted = early.Process(Arrive(PRESENT.AddMinutes(-1)), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, rejected.Outcome);
        Assert.Null(late.Phases[0].ArriveTime);
        Assert.Equal(TimeEventOutcome.Accepted, accepted.Outcome);
        Assert.Equal(PRESENT.AddMinutes(-1), early.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_snapshot_for_a_complete_final_phase_is_recorded_on_it_as_rejected_because_the_participation_is_complete()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE, present: PRESENT, isFinal: true));

        var arrive = participation.Process(Arrive(PRESENT.AddHours(1)), ACTOR, RECORDED);
        var present = participation.Process(Present(PRESENT.AddHours(2)), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedParticipationComplete, arrive.Outcome);
        Assert.IsType<Arrived>(arrive);
        Assert.Equal(TimeEventOutcome.RejectedParticipationComplete, present.Outcome);
        Assert.IsType<Presented>(present);
        Assert.Equal(4, participation.Phases[0].Events.Count);
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_final_snapshot_is_an_arrival_and_the_finish_line_of_a_phase_that_is_not_final_is_refused_when_it_is_separate()
    {
        var phase = CreatePhase(isFinal: false);
        phase.IsSeparateFinish = true;
        var participation = Ride(phase);
        var final = new Snapshot(1, SnapshotType.Final, SnapshotMethod.Manual, new Timestamp(ARRIVE));

        var recorded = participation.Process(final, ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedSeparateStageLine, recorded.Outcome);
        Assert.IsType<Arrived>(recorded);
        Assert.Null(participation.Phases[0].ArriveTime);
    }

    [Fact]
    public void An_arrive_at_the_final_phase_is_refused_when_the_finish_is_separate()
    {
        var phase = CreatePhase(isFinal: true);
        phase.IsSeparateFinish = true;
        var participation = Ride(phase);

        var recorded = participation.Process(Arrive(ARRIVE), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedSeparateFinishLine, recorded.Outcome);
        Assert.Null(participation.Phases[0].ArriveTime);
    }

    [Fact]
    public void A_final_snapshot_is_accepted_as_the_arrival_of_the_final_phase()
    {
        var participation = Ride(CreatePhase(isFinal: true));
        var final = new Snapshot(1, SnapshotType.Final, SnapshotMethod.Manual, new Timestamp(ARRIVE));

        var recorded = participation.Process(final, ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.Accepted, recorded.Outcome);
        Assert.IsType<Arrived>(recorded);
        Assert.Equal(ARRIVE, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void Every_event_a_snapshot_leaves_carries_the_actor_and_the_time_it_was_recorded_at_whatever_its_outcome()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));

        var events = new[]
        {
            participation.Process(Arrive(ARRIVE.AddMinutes(5)), ACTOR, RECORDED),
            participation.Process(Present(ARRIVE.AddMinutes(-5)), ACTOR, RECORDED.AddMinutes(1)),
            participation.Process(Present(PRESENT), TestId.Of(10), RECORDED.AddMinutes(2)),
        };

        Assert.Equal([ACTOR, ACTOR, TestId.Of(10)], events.Select(x => x.ActorId).ToArray());
        Assert.Equal(
            [RECORDED, RECORDED.AddMinutes(1), RECORDED.AddMinutes(2)],
            events.Select(x => x.RecordedAt).ToArray()
        );
        Assert.Equal(
            [TimeEventOutcome.RejectedDuplicateArrive, TimeEventOutcome.RejectedInvalidTime, TimeEventOutcome.Accepted],
            events.Select(x => x.Outcome).ToArray()
        );
    }

    static Snapshot Arrive(DateTimeOffset time, SnapshotMethod method = SnapshotMethod.Manual)
    {
        return new Snapshot(1, SnapshotType.Arrive, method, new Timestamp(time));
    }

    static Snapshot Present(DateTimeOffset time, SnapshotMethod method = SnapshotMethod.Manual)
    {
        return new Snapshot(1, SnapshotType.Present, method, new Timestamp(time));
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
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        DateTimeOffset? represent = null,
        bool isRepresentRequested = false,
        bool isFinal = false,
        TimeSpan? compulsoryThreshold = null
    )
    {
        return new Phase(
            "",
            20,
            40,
            isFinal ? null : 40,
            CompetitionRuleset.Regional,
            isFinal,
            compulsoryThreshold,
            Timestamp.Create(START),
            Timestamp.Create(arrive),
            Timestamp.Create(present),
            Timestamp.Create(represent),
            isRepresentRequested,
            false,
            false
        );
    }
}
