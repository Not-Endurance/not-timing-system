using Not.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// What an Official's Update of a Snapshot they sent does to the Participation (#644, ADR-0005, ADR-0013): it sets an
/// absolute value, so the last write wins, and it is an event of its own on the Phase its new time belongs to, accepted or
/// not. It replaces the latest accepted time of its kind, and a Present Update changes the Presentation, or the
/// Representation when the Phase has one, and never makes a re-presentation. What it answers is what changed, or why not.
/// The Phase of the first ride starts at 08:00 and its Rest is 40 minutes, as in the tests of the Snapshots.
/// </summary>
public sealed class ParticipationSnapshotUpdateTests
{
    static readonly DateTimeOffset START = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddHours(1);
    static readonly DateTimeOffset PRESENT = ARRIVE.AddMinutes(10);
    static readonly DateTimeOffset RECORDED = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly Guid ACTOR = TestId.Of(9);
    static readonly Guid SENT = TestId.Of(70);

    [Fact]
    public void A_snapshot_is_recorded_as_the_event_that_carries_its_id()
    {
        var participation = Ride(CreatePhase());

        var recorded = participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);

        Assert.Equal(SENT, recorded.Id);
        Assert.Same(recorded, Assert.Single(participation.Phases[0].Events));
    }

    [Fact]
    public void A_snapshot_with_no_id_is_recorded_as_an_event_of_an_id_of_its_own()
    {
        var participation = Ride(CreatePhase());

        var recorded = participation.Process(Arrive(ARRIVE), ACTOR, RECORDED);

        Assert.NotEqual(Guid.Empty, recorded.Id);
    }

    [Fact]
    public void A_rejected_snapshot_carries_its_id_too()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));

        var recorded = participation.Process(Arrive(ARRIVE.AddMinutes(5), SENT), ACTOR, RECORDED);

        Assert.Equal(TimeEventOutcome.RejectedDuplicateArrive, recorded.Outcome);
        Assert.Equal(SENT, recorded.Id);
    }

    [Fact]
    public void An_arrive_update_replaces_the_arrival_the_phase_shows_and_keeps_the_event_it_replaces()
    {
        var participation = Ride(CreatePhase());
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        var corrected = ARRIVE.AddMinutes(3);

        var update = participation.UpdateSnapshot(
            SENT,
            new Timestamp(corrected),
            TestId.Of(10),
            RECORDED.AddMinutes(1)
        );

        Assert.True(update.Event.IsAccepted);
        Assert.IsType<ArriveUpdated>(update.Event);
        Assert.Equal(TimeSlot.Arrive, update.Slot);
        Assert.Same(participation.Phases[0], update.Phase);
        Assert.Equal(ARRIVE, update.Previous!.ToDateTimeOffset());
        Assert.Equal(corrected, update.Current!.ToDateTimeOffset());
        Assert.Equal(corrected, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Equal(2, participation.Phases[0].Events.Count);
        Assert.IsType<Arrived>(participation.Phases[0].Events[0]);
        Assert.True(participation.Phases[0].Events[0].IsAccepted);
    }

    [Fact]
    public void An_update_is_recorded_with_the_user_that_made_it_the_instant_it_was_recorded_and_the_manual_method()
    {
        var participation = Ride(CreatePhase());
        participation.Process(Arrive(ARRIVE, SENT, SnapshotMethod.RFID), ACTOR, RECORDED);

        var update = participation.UpdateSnapshot(
            SENT,
            new Timestamp(ARRIVE.AddMinutes(3)),
            TestId.Of(10),
            RECORDED.AddMinutes(1)
        );

        Assert.Equal(TestId.Of(10), update.Event.ActorId);
        Assert.Equal(RECORDED.AddMinutes(1), update.Event.RecordedAt);
        Assert.Equal(SnapshotMethod.Manual, update.Event.Method);
        Assert.NotEqual(SENT, update.Event.Id);
        Assert.Equal(ARRIVE.AddMinutes(3), update.Event.Time.ToDateTimeOffset());
    }

    [Fact]
    public void Of_two_updates_the_last_one_wins()
    {
        var participation = Ride(CreatePhase());
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);

        participation.UpdateSnapshot(SENT, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED.AddMinutes(1));
        var second = participation.UpdateSnapshot(
            SENT,
            new Timestamp(ARRIVE.AddMinutes(5)),
            ACTOR,
            RECORDED.AddMinutes(2)
        );

        Assert.Equal(ARRIVE.AddMinutes(3), second.Previous!.ToDateTimeOffset());
        Assert.Equal(ARRIVE.AddMinutes(5), participation.Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Equal(3, participation.Phases[0].Events.Count);
    }

    [Fact]
    public void A_present_update_replaces_the_presentation_and_is_not_marked_as_a_representation()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));
        participation.Process(Present(PRESENT, SENT), ACTOR, RECORDED);
        var corrected = PRESENT.AddMinutes(2);

        var update = participation.UpdateSnapshot(SENT, new Timestamp(corrected), ACTOR, RECORDED.AddMinutes(1));

        var updated = Assert.IsType<PresentUpdated>(update.Event);
        Assert.False(updated.IsRepresent);
        Assert.True(updated.IsAccepted);
        Assert.Equal(TimeSlot.Present, update.Slot);
        Assert.Equal(PRESENT, update.Previous!.ToDateTimeOffset());
        Assert.Equal(corrected, update.Current!.ToDateTimeOffset());
        Assert.Equal(corrected, participation.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Null(participation.Phases[0].RepresentTime);
    }

    [Fact]
    public void A_present_update_changes_the_representation_when_the_phase_has_one_and_makes_no_new_representation()
    {
        var represent = PRESENT.AddMinutes(30);
        var participation = Ride(
            CreatePhase(arrive: ARRIVE, present: PRESENT, represent: represent, isRepresentRequested: true)
        );
        var presentation = participation.Phases[0].Events.OfType<Presented>().First(x => !x.IsRepresent);
        var corrected = represent.AddMinutes(4);

        var update = participation.UpdateSnapshot(presentation.Id, new Timestamp(corrected), ACTOR, RECORDED);

        var updated = Assert.IsType<PresentUpdated>(update.Event);
        Assert.True(updated.IsRepresent);
        Assert.Equal(TimeSlot.Represent, update.Slot);
        Assert.Equal(represent, update.Previous!.ToDateTimeOffset());
        Assert.Equal(corrected, participation.Phases[0].RepresentTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Equal(2, participation.Phases[0].Events.OfType<Presented>().Count());
    }

    [Fact]
    public void An_update_into_an_empty_slot_becomes_the_time_when_the_snapshot_it_names_was_rejected()
    {
        var participation = Ride(CreatePhase());
        var rejected = participation.Process(Arrive(START.AddMinutes(-5), SENT), ACTOR, RECORDED);

        var update = participation.UpdateSnapshot(SENT, new Timestamp(ARRIVE), ACTOR, RECORDED.AddMinutes(1));

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, rejected.Outcome);
        Assert.True(update.Event.IsAccepted);
        Assert.Null(update.Previous);
        Assert.Equal(ARRIVE, update.Current!.ToDateTimeOffset());
        Assert.Equal(ARRIVE, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_update_with_no_presentation_to_replace_is_an_unmarked_presentation()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));
        participation.Process(Present(ARRIVE.AddMinutes(-3), SENT), ACTOR, RECORDED); // rejected: before the arrival

        var update = participation.UpdateSnapshot(SENT, new Timestamp(PRESENT), ACTOR, RECORDED.AddMinutes(1));

        Assert.False(Assert.IsType<PresentUpdated>(update.Event).IsRepresent);
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-60)]
    public void An_arrive_update_before_the_start_is_recorded_as_a_rejected_invalid_time_and_the_time_stays(
        int minutesFromTheStart
    )
    {
        var participation = Ride(CreatePhase());
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);

        var update = participation.UpdateSnapshot(
            SENT,
            new Timestamp(START.AddMinutes(minutesFromTheStart)),
            ACTOR,
            RECORDED.AddMinutes(1)
        );

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, update.Event.Outcome);
        Assert.IsType<ArriveUpdated>(update.Event);
        Assert.Equal(ARRIVE, update.Previous!.ToDateTimeOffset());
        Assert.Equal(ARRIVE, update.Current!.ToDateTimeOffset());
        Assert.Equal(ARRIVE, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Equal(2, participation.Phases[0].Events.Count);
    }

    [Fact]
    public void A_present_update_at_or_before_the_arrival_is_recorded_as_a_rejected_invalid_time()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));
        participation.Process(Present(PRESENT, SENT), ACTOR, RECORDED);

        var atTheArrival = participation.UpdateSnapshot(SENT, new Timestamp(ARRIVE), ACTOR, RECORDED.AddMinutes(1));
        var before = participation.UpdateSnapshot(
            SENT,
            new Timestamp(ARRIVE.AddMinutes(-2)),
            ACTOR,
            RECORDED.AddMinutes(2)
        );

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, atTheArrival.Event.Outcome);
        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, before.Event.Outcome);
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_arrive_update_that_reaches_the_presentation_is_recorded_as_a_rejected_invalid_time()
    {
        var participation = Ride(CreatePhase());
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        participation.Process(Present(PRESENT), ACTOR, RECORDED.AddMinutes(1));

        var reaching = participation.UpdateSnapshot(SENT, new Timestamp(PRESENT), ACTOR, RECORDED.AddMinutes(2));
        var before = participation.UpdateSnapshot(
            SENT,
            new Timestamp(PRESENT.AddMinutes(-1)),
            ACTOR,
            RECORDED.AddMinutes(3)
        );

        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, reaching.Event.Outcome);
        Assert.Equal(TimeEventOutcome.Accepted, before.Event.Outcome);
        Assert.Equal(PRESENT.AddMinutes(-1), participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_update_of_a_participation_that_is_complete_is_taken_because_a_time_that_is_there_may_be_corrected()
    {
        var participation = Ride(CreatePhase(isFinal: true));
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        participation.Process(Present(PRESENT), ACTOR, RECORDED.AddMinutes(1));
        Assert.True(participation.IsComplete());

        var update = participation.UpdateSnapshot(
            SENT,
            new Timestamp(ARRIVE.AddMinutes(2)),
            ACTOR,
            RECORDED.AddMinutes(2)
        );

        Assert.True(update.Event.IsAccepted);
        Assert.Equal(ARRIVE.AddMinutes(2), participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_arrive_update_leaves_the_out_time_that_follows_the_presentation_and_the_next_start_where_they_are()
    {
        var participation = Ride(CreatePhase(), CreateSecondPhase());
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        participation.Process(Present(PRESENT), ACTOR, RECORDED.AddMinutes(1));
        var outBefore = participation.Phases[0].GetOutTime();

        var update = participation.UpdateSnapshot(
            SENT,
            new Timestamp(ARRIVE.AddMinutes(2)),
            ACTOR,
            RECORDED.AddMinutes(2)
        );

        Assert.True(update.Event.IsAccepted);
        Assert.Equal(outBefore, participation.Phases[0].GetOutTime());
        Assert.Equal(outBefore, participation.Phases[1].StartTime);
    }

    [Fact]
    public void An_accepted_present_update_moves_the_out_time_and_the_start_of_the_next_phase_with_it()
    {
        var participation = Ride(CreatePhase(), CreateSecondPhase());
        participation.Process(Arrive(ARRIVE, TestId.Of(71)), ACTOR, RECORDED);
        participation.Process(Present(PRESENT, SENT), ACTOR, RECORDED.AddMinutes(1));
        var outBefore = participation.Phases[0].GetOutTime();

        participation.UpdateSnapshot(SENT, new Timestamp(PRESENT.AddMinutes(4)), ACTOR, RECORDED.AddMinutes(2));

        Assert.NotEqual(outBefore, participation.Phases[0].GetOutTime());
        Assert.Equal(PRESENT.AddMinutes(4).AddMinutes(40), participation.Phases[0].GetOutTime()!.ToDateTimeOffset());
        Assert.Equal(participation.Phases[0].GetOutTime(), participation.Phases[1].StartTime);
    }

    [Fact]
    public void An_update_stamped_at_or_after_the_start_of_the_next_phase_is_recorded_in_the_next_phase()
    {
        var participation = Ride(CreatePhase(), CreateSecondPhase());
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        participation.Process(Present(PRESENT), ACTOR, RECORDED.AddMinutes(1));
        var nextStart = participation.Phases[1].StartTime!.ToDateTimeOffset();
        var late = nextStart.AddMinutes(20);

        var update = participation.UpdateSnapshot(SENT, new Timestamp(late), ACTOR, RECORDED.AddMinutes(2));

        Assert.Same(participation.Phases[1], update.Phase);
        Assert.Same(update.Event, Assert.Single(participation.Phases[1].Events));
        Assert.Equal(late, participation.Phases[1].ArriveTime!.ToDateTimeOffset());
        Assert.Equal(ARRIVE, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Same(participation.Phases[1], participation.Phases.Current);
    }

    [Fact]
    public void An_update_that_brings_the_recovery_under_the_limit_restores_a_participation_eliminated_for_time()
    {
        var participation = Ride(CreatePhase(maxRecovery: 20));
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        participation.Process(Present(ARRIVE.AddMinutes(25)), ACTOR, RECORDED.AddMinutes(1));
        Assert.True(participation.IsEliminated());

        var update = participation.UpdateSnapshot(
            SENT,
            new Timestamp(ARRIVE.AddMinutes(10)),
            ACTOR,
            RECORDED.AddMinutes(2)
        );

        Assert.True(update.Event.IsAccepted);
        Assert.False(participation.IsEliminated());
    }

    [Fact]
    public void An_elimination_for_time_that_was_stored_and_read_again_is_restored_when_the_time_is_corrected()
    {
        var phase = CreatePhase(arrive: ARRIVE, present: ARRIVE.AddMinutes(25), maxRecovery: 20);
        var stored = new FailedToQualify([FailToQualifyCode.OT]); // an instance of its own, as a document makes it
        var participation = Ride(stored, phase);
        var arrival = phase.Events.OfType<Arrived>().Single();

        var update = participation.UpdateSnapshot(arrival.Id, new Timestamp(ARRIVE.AddMinutes(10)), ACTOR, RECORDED);

        Assert.True(update.Event.IsAccepted);
        Assert.Null(participation.Eliminated);
    }

    [Fact]
    public void An_elimination_that_somebody_gave_for_another_reason_with_the_same_code_is_kept()
    {
        var phase = CreatePhase(arrive: ARRIVE, present: ARRIVE.AddMinutes(25), maxRecovery: 20);
        var given = new FailedToQualify([FailToQualifyCode.OT], "Judged by the Ground Jury");
        var participation = Ride(given, phase);
        var arrival = phase.Events.OfType<Arrived>().Single();

        participation.UpdateSnapshot(arrival.Id, new Timestamp(ARRIVE.AddMinutes(10)), ACTOR, RECORDED);

        Assert.Same(given, participation.Eliminated);
    }

    [Fact]
    public void An_elimination_for_the_speed_restriction_that_somebody_gave_is_kept_whatever_the_times_say()
    {
        var phase = CreatePhase(arrive: ARRIVE, present: ARRIVE.AddMinutes(25), maxRecovery: 20);
        var given = new FailedToQualify([FailToQualifyCode.SP]);
        var participation = Ride(given, phase);
        var arrival = phase.Events.OfType<Arrived>().Single();

        participation.UpdateSnapshot(arrival.Id, new Timestamp(ARRIVE.AddMinutes(10)), ACTOR, RECORDED);

        Assert.Same(given, participation.Eliminated);
    }

    [Fact]
    public void An_update_that_takes_the_recovery_over_the_limit_eliminates_the_participation_for_time()
    {
        var participation = Ride(CreatePhase(maxRecovery: 20));
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        participation.Process(Present(ARRIVE.AddMinutes(15)), ACTOR, RECORDED.AddMinutes(1));
        Assert.False(participation.IsEliminated());

        participation.UpdateSnapshot(SENT, new Timestamp(ARRIVE.AddMinutes(-10)), ACTOR, RECORDED.AddMinutes(2));

        Assert.True(participation.IsEliminated());
    }

    [Fact]
    public void An_update_leaves_an_elimination_that_is_not_for_time_as_it_is()
    {
        var participation = Ride(CreatePhase(maxRecovery: 20));
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        participation.Process(Present(ARRIVE.AddMinutes(15)), ACTOR, RECORDED.AddMinutes(1));
        participation.Withdraw();

        var update = participation.UpdateSnapshot(
            SENT,
            new Timestamp(ARRIVE.AddMinutes(-10)),
            ACTOR,
            RECORDED.AddMinutes(2)
        );

        Assert.True(update.Event.IsAccepted);
        Assert.IsType<Withdrawn>(participation.Eliminated);
    }

    [Fact]
    public void A_phase_that_an_update_completes_announces_its_completion_once()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));
        participation.Process(Present(PRESENT.AddMinutes(-20), SENT), ACTOR, RECORDED); // rejected: not after the arrival
        participation.DequeueDomainEvents();

        participation.UpdateSnapshot(SENT, new Timestamp(PRESENT), ACTOR, RECORDED.AddMinutes(1));

        Assert.Single(participation.DequeueDomainEvents());
    }

    [Fact]
    public void An_update_that_is_rejected_changes_nothing_else_and_announces_nothing()
    {
        var participation = Ride(CreatePhase(), CreateSecondPhase());
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        participation.Process(Present(PRESENT), ACTOR, RECORDED.AddMinutes(1));
        var startOfTheNext = participation.Phases[1].StartTime;
        participation.DequeueDomainEvents();

        participation.UpdateSnapshot(SENT, new Timestamp(START.AddMinutes(-1)), ACTOR, RECORDED.AddMinutes(2));

        Assert.Equal(startOfTheNext, participation.Phases[1].StartTime);
        Assert.Empty(participation.DequeueDomainEvents());
    }

    [Fact]
    public void An_update_of_a_snapshot_the_participation_does_not_hold_is_refused()
    {
        var participation = Ride(CreatePhase());
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);

        var refused = Assert.Throws<GuardException>(
            () => participation.UpdateSnapshot(TestId.Of(80), new Timestamp(ARRIVE), ACTOR, RECORDED)
        );

        Assert.Contains(TestId.Of(80).ToString(), refused.Message);
        Assert.Single(participation.Phases[0].Events);
    }

    [Fact]
    public void An_update_names_the_snapshot_whichever_phase_holds_it_and_the_updates_it_got_name_it_too()
    {
        var participation = Ride(CreatePhase(), CreateSecondPhase());
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        participation.Process(Present(PRESENT), ACTOR, RECORDED.AddMinutes(1));
        var first = participation.UpdateSnapshot(
            SENT,
            new Timestamp(ARRIVE.AddMinutes(1)),
            ACTOR,
            RECORDED.AddMinutes(2)
        );

        var again = participation.UpdateSnapshot(
            first.Event.Id,
            new Timestamp(ARRIVE.AddMinutes(2)),
            ACTOR,
            RECORDED.AddMinutes(3)
        );

        Assert.IsType<ArriveUpdated>(again.Event);
        Assert.Equal(ARRIVE.AddMinutes(2), participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(15, true)]
    [InlineData(20, true)]
    public void An_update_that_takes_the_recovery_to_the_compulsory_threshold_makes_the_required_inspection_compulsory(
        int recoveryMinutes,
        bool isCompulsory
    )
    {
        var participation = Ride(
            CreatePhase(arrive: ARRIVE, present: ARRIVE.AddMinutes(5), compulsoryThreshold: TimeSpan.FromMinutes(15))
        );
        var presentation = participation.Phases[0].Events.OfType<Presented>().Single();

        participation.UpdateSnapshot(
            presentation.Id,
            new Timestamp(ARRIVE.AddMinutes(recoveryMinutes)),
            ACTOR,
            RECORDED
        );

        Assert.Equal(isCompulsory, participation.Phases[0].IsRequiredInspectionCompulsory);
    }

    [Fact]
    public void An_update_that_brings_the_recovery_under_the_compulsory_threshold_lifts_the_compulsory_required_inspection()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE, compulsoryThreshold: TimeSpan.FromMinutes(15)));
        participation.Process(Present(ARRIVE.AddMinutes(20), SENT), ACTOR, RECORDED);
        Assert.True(participation.Phases[0].IsRequiredInspectionCompulsory);

        participation.UpdateSnapshot(SENT, new Timestamp(ARRIVE.AddMinutes(10)), ACTOR, RECORDED.AddMinutes(1));

        Assert.False(participation.Phases[0].IsRequiredInspectionCompulsory);
    }

    [Fact]
    public void An_update_of_a_presentation_changes_the_one_that_was_accepted_and_not_a_representation_that_was_refused()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE, present: PRESENT, isRepresentRequested: true));
        participation.Process(Present(PRESENT.AddMinutes(-2), SENT), ACTOR, RECORDED); // a representation before the presentation
        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, participation.Phases[0].Events[^1].Outcome);

        var update = participation.UpdateSnapshot(
            SENT,
            new Timestamp(PRESENT.AddMinutes(3)),
            ACTOR,
            RECORDED.AddMinutes(1)
        );

        Assert.True(update.IsAccepted);
        Assert.Equal(TimeSlot.Present, update.Slot);
        Assert.Equal(PRESENT.AddMinutes(3), participation.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Null(participation.Phases[0].RepresentTime);
    }

    [Fact]
    public void An_update_changes_the_representation_when_the_latest_presentation_accepted_is_an_update_of_one()
    {
        var phase = new Phase(
            "",
            20,
            40,
            40,
            CompetitionRuleset.Regional,
            false,
            null,
            Timestamp.Create(START),
            [
                new Arrived(new Timestamp(ARRIVE), TimeEventOutcome.Accepted, SnapshotMethod.Manual, null, null),
                new Presented(
                    new Timestamp(PRESENT),
                    false,
                    TimeEventOutcome.Accepted,
                    SnapshotMethod.Manual,
                    null,
                    null
                ),
                new PresentUpdated(
                    new Timestamp(PRESENT.AddMinutes(20)),
                    true,
                    TimeEventOutcome.Accepted,
                    SnapshotMethod.Manual,
                    null,
                    null,
                    SENT
                ),
            ],
            true,
            false,
            false
        );
        var participation = Ride(phase);

        var update = participation.UpdateSnapshot(SENT, new Timestamp(PRESENT.AddMinutes(25)), ACTOR, RECORDED);

        Assert.True(update.IsAccepted);
        Assert.Equal(TimeSlot.Represent, update.Slot);
        Assert.Equal(PRESENT.AddMinutes(25), phase.RepresentTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, phase.PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_update_of_a_presentation_names_it_by_the_id_of_the_update_that_changed_it_too()
    {
        var participation = Ride(CreatePhase(arrive: ARRIVE));
        participation.Process(Present(PRESENT, SENT), ACTOR, RECORDED);
        var first = participation.UpdateSnapshot(
            SENT,
            new Timestamp(PRESENT.AddMinutes(1)),
            ACTOR,
            RECORDED.AddMinutes(1)
        );

        var again = participation.UpdateSnapshot(
            first.Event.Id,
            new Timestamp(PRESENT.AddMinutes(2)),
            ACTOR,
            RECORDED.AddMinutes(2)
        );

        Assert.IsType<PresentUpdated>(again.Event);
        Assert.Equal(TimeSlot.Present, again.Slot);
        Assert.Equal(PRESENT.AddMinutes(2), participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void The_time_a_phase_shows_is_the_latest_accepted_event_that_feeds_it_whichever_kind_it_is()
    {
        var participation = Ride(CreatePhase());
        participation.Process(Arrive(ARRIVE, SENT), ACTOR, RECORDED);
        participation.UpdateSnapshot(SENT, new Timestamp(ARRIVE.AddMinutes(6)), ACTOR, RECORDED.AddMinutes(1));
        participation.UpdateSnapshot(SENT, new Timestamp(START.AddMinutes(-3)), ACTOR, RECORDED.AddMinutes(2)); // rejected
        var phase = participation.Phases[0];

        Assert.Equal(ARRIVE.AddMinutes(6), phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(
            [
                (typeof(Arrived), TimeEventOutcome.Accepted),
                (typeof(ArriveUpdated), TimeEventOutcome.Accepted),
                (typeof(ArriveUpdated), TimeEventOutcome.RejectedInvalidTime),
            ],
            phase.Events.Select(x => (x.GetType(), x.Outcome)).ToArray()
        );
    }

    static Snapshot Arrive(DateTimeOffset time, Guid? id = null, SnapshotMethod method = SnapshotMethod.Manual)
    {
        return new Snapshot(1, SnapshotType.Arrive, method, new Timestamp(time), id);
    }

    static Snapshot Present(DateTimeOffset time, Guid? id = null, SnapshotMethod method = SnapshotMethod.Manual)
    {
        return new Snapshot(1, SnapshotType.Present, method, new Timestamp(time), id);
    }

    static Participation Ride(params Phase[] phases)
    {
        return Ride(null, phases);
    }

    static Participation Ride(Eliminated? eliminated, params Phase[] phases)
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
            eliminated,
            eventId: TestId.Of(1)
        );
    }

    /// <summary>The first Phase: it starts at 08:00 and is not the last, so its Out time is the presentation and the Rest after it.</summary>
    static Phase CreatePhase(
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        DateTimeOffset? represent = null,
        bool isRepresentRequested = false,
        bool isFinal = false,
        int maxRecovery = 40,
        TimeSpan? compulsoryThreshold = null
    )
    {
        return new Phase(
            "",
            20,
            maxRecovery,
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

    /// <summary>The second and last Phase: nothing has reached it yet, and it starts when the first is out.</summary>
    static Phase CreateSecondPhase()
    {
        return new Phase(
            "",
            20,
            40,
            null,
            CompetitionRuleset.Regional,
            true,
            null,
            null,
            (Timestamp?)null,
            null,
            null,
            false,
            false,
            false
        );
    }
}
