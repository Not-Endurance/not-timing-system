using Not.Domain.Exceptions;
using Not.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Localization.NtsStrings;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// What the Main Operator's disabling and enabling of time events does to a Participation (#619, ADR-0005): a disabled event
/// stays in the history with the outcome of a manual reject and when it was disabled, so that the previous accepted time of its
/// kind takes over again, and it can be enabled again. Only an event that is accepted is disabled and only one that was
/// disabled is enabled: what the rules rejected stays as it is. A change is made whole or not at all, is evaluated once like
/// any accepted change, and is refused when it would leave an earlier Phase incomplete while a later one has times. Every case
/// is the same ride: Phase 1 starts at 08:00, its Arrival is at 09:00, its Presentation at 09:10, its recovery limit is 40
/// minutes and its Rest 40 minutes, so it is out at 09:50, which is the Start of Phase 2.
/// </summary>
public sealed class ParticipationTimelineTests
{
    static readonly DateTimeOffset START = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddHours(1);
    static readonly DateTimeOffset PRESENT = ARRIVE.AddMinutes(10);
    static readonly DateTimeOffset OUT = PRESENT.AddMinutes(40);
    static readonly DateTimeOffset RECORDED = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset DISABLED = RECORDED.AddMinutes(30);
    static readonly Guid ACTOR = TestId.Of(9);
    static readonly Guid ARRIVED = TestId.Of(70);
    static readonly Guid PRESENTED = TestId.Of(71);

    [Fact]
    public void Disabling_an_update_makes_the_time_it_replaced_take_over_again_and_records_when()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        var update = participation.UpdateSnapshot(ARRIVED, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED).Event;

        participation.ChangeTimeEvents([update.Id], [], DISABLED);

        var phase = participation.Phases[0];
        Assert.Equal(ARRIVE, phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(TimeEventOutcome.RejectedManually, update.Outcome);
        Assert.Equal(DISABLED, update.DisabledAt);
        Assert.Equal(2, phase.Events.Count); // it stays in the history
        Assert.Null(phase.Events[0].DisabledAt);
    }

    [Fact]
    public void A_phase_shows_the_time_of_the_latest_accepted_event_that_feeds_it_and_of_no_other()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        var arrived = Process(participation, Arrive(ARRIVE, ARRIVED));
        var duplicate = Process(participation, Arrive(ARRIVE.AddMinutes(1), TestId.Of(72)));
        var update = participation.UpdateSnapshot(ARRIVED, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED).Event;
        var phase = participation.Phases[0];

        var presented = Process(participation, Present(PRESENT, PRESENTED));

        Assert.False(phase.Shows(arrived)); // the update replaced it
        Assert.False(phase.Shows(duplicate)); // the rules rejected it
        Assert.True(phase.Shows(update));
        Assert.True(phase.Shows(presented)); // the time of another kind is shown beside it

        participation.ChangeTimeEvents([update.Id], [], DISABLED);

        Assert.True(phase.Shows(arrived));
        Assert.False(phase.Shows(update)); // it was disabled
    }

    [Fact]
    public void Disabling_the_only_event_of_a_time_takes_the_time_away()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));

        participation.ChangeTimeEvents([ARRIVED], [], DISABLED);

        Assert.Null(participation.Phases[0].ArriveTime);
    }

    [Fact]
    public void Enabling_a_disabled_event_restores_it_and_forgets_when_it_was_disabled()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        var update = participation.UpdateSnapshot(ARRIVED, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED).Event;
        participation.ChangeTimeEvents([update.Id], [], DISABLED);

        participation.ChangeTimeEvents([], [update.Id], DISABLED.AddMinutes(1));

        Assert.Equal(TimeEventOutcome.Accepted, update.Outcome);
        Assert.Null(update.DisabledAt);
        Assert.Equal(ARRIVE.AddMinutes(3), participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void Disabling_and_enabling_events_in_one_change_applies_both()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var update = participation.UpdateSnapshot(ARRIVED, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED).Event;
        participation.ChangeTimeEvents([update.Id], [], DISABLED);

        participation.ChangeTimeEvents([PRESENTED], [update.Id], DISABLED.AddMinutes(1));

        var phase = participation.Phases[0];
        Assert.Equal(ARRIVE.AddMinutes(3), phase.ArriveTime!.ToDateTimeOffset());
        Assert.Null(phase.PresentTime);
    }

    [Fact]
    public void An_event_the_rules_rejected_is_neither_disabled_nor_enabled()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE));
        var duplicate = Process(participation, Arrive(ARRIVE.AddMinutes(2), TestId.Of(72)));
        Assert.Equal(TimeEventOutcome.RejectedDuplicateArrive, duplicate.Outcome);

        var disabling = Assert.Throws<DomainException>(
            () => participation.ChangeTimeEvents([duplicate.Id], [], DISABLED)
        );
        var enabling = Assert.Throws<DomainException>(
            () => participation.ChangeTimeEvents([], [duplicate.Id], DISABLED)
        );
        Assert.Equal(Time_event_set_aside_by_the_rules_string, disabling.Message); // it is told apart from one that changed
        Assert.Equal(Time_event_set_aside_by_the_rules_string, enabling.Message);

        Assert.Equal(TimeEventOutcome.RejectedDuplicateArrive, duplicate.Outcome);
        Assert.Null(duplicate.DisabledAt);
    }

    [Fact]
    public void An_event_that_is_not_there_is_refused()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );

        var disabling = Assert.Throws<DomainException>(
            () => participation.ChangeTimeEvents([TestId.Of(99)], [], DISABLED)
        );
        var enabling = Assert.Throws<DomainException>(
            () => participation.ChangeTimeEvents([], [TestId.Of(99)], DISABLED)
        );
        Assert.Equal(Time_event_changed_string, disabling.Message);
        Assert.Equal(Time_event_changed_string, enabling.Message);
    }

    [Fact]
    public void An_event_that_is_not_as_it_was_when_it_was_chosen_is_refused()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        var accepted = Process(participation, Arrive(ARRIVE, ARRIVED));
        var update = participation.UpdateSnapshot(ARRIVED, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED).Event;
        participation.ChangeTimeEvents([update.Id], [], DISABLED);

        var disabled = Assert.Throws<DomainException>(() => participation.ChangeTimeEvents([update.Id], [], DISABLED)); // already disabled
        var accepting = Assert.Throws<DomainException>(
            () => participation.ChangeTimeEvents([], [accepted.Id], DISABLED)
        ); // not disabled
        Assert.Equal(Time_event_changed_string, disabled.Message);
        Assert.Equal(Time_event_changed_string, accepting.Message);
    }

    [Fact]
    public void A_change_is_made_whole_or_not_at_all()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        var arrived = Process(participation, Arrive(ARRIVE, ARRIVED));
        var duplicate = Process(participation, Arrive(ARRIVE.AddMinutes(2), TestId.Of(72)));

        Assert.Throws<DomainException>(() => participation.ChangeTimeEvents([arrived.Id], [duplicate.Id], DISABLED));

        Assert.Equal(TimeEventOutcome.Accepted, arrived.Outcome);
        Assert.Equal(ARRIVE, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_event_that_is_both_disabled_and_enabled_is_a_mistake_of_the_caller()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        var arrived = participation.Phases[0].Events[0];

        Assert.Throws<GuardException>(() => participation.ChangeTimeEvents([arrived.Id], [arrived.Id], DISABLED));
    }

    [Fact]
    public void Nothing_chosen_changes_and_announces_nothing()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT, id: TestId.Of(2))
        );

        participation.ChangeTimeEvents([], [], DISABLED);

        Assert.Empty(participation.DequeueDomainEvents());
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void Disabling_the_late_presentation_lifts_the_elimination_for_time_and_enabling_it_brings_it_back()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var late = participation.UpdateSnapshot(PRESENTED, new Timestamp(ARRIVE.AddMinutes(45)), ACTOR, RECORDED).Event;
        Assert.IsType<FailedToQualify>(participation.Eliminated);

        participation.ChangeTimeEvents([late.Id], [], DISABLED);

        Assert.Null(participation.Eliminated);
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());

        participation.ChangeTimeEvents([], [late.Id], DISABLED.AddMinutes(1));

        var eliminated = Assert.IsType<FailedToQualify>(participation.Eliminated);
        Assert.Equal([FailToQualifyCode.OT], eliminated.FtqCodes);
    }

    [Fact]
    public void The_inspection_that_a_long_recovery_makes_compulsory_follows_the_times_the_change_leaves()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1), compulsoryThreshold: TimeSpan.FromMinutes(30)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var late = participation.UpdateSnapshot(PRESENTED, new Timestamp(ARRIVE.AddMinutes(35)), ACTOR, RECORDED).Event;
        Assert.True(participation.Phases[0].IsRequiredInspectionCompulsory);

        participation.ChangeTimeEvents([late.Id], [], DISABLED);

        Assert.False(participation.Phases[0].IsRequiredInspectionCompulsory);

        participation.ChangeTimeEvents([], [late.Id], DISABLED.AddMinutes(1));

        Assert.True(participation.Phases[0].IsRequiredInspectionCompulsory);
    }

    [Fact]
    public void An_elimination_that_a_person_gave_is_left_as_it_is()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var late = participation.UpdateSnapshot(PRESENTED, new Timestamp(ARRIVE.AddMinutes(45)), ACTOR, RECORDED).Event;
        participation.Withdraw();

        participation.ChangeTimeEvents([late.Id], [], DISABLED);

        Assert.IsType<Withdrawn>(participation.Eliminated);
    }

    [Fact]
    public void A_phase_that_still_runs_over_the_limit_keeps_the_participation_eliminated_for_time_whichever_phase_is_changed()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        participation.UpdateSnapshot(PRESENTED, new Timestamp(ARRIVE.AddMinutes(45)), ACTOR, RECORDED); // Phase 1 is over the limit
        Assert.IsType<FailedToQualify>(participation.Eliminated);
        var next = Process(participation, Arrive(OUT.AddMinutes(30), TestId.Of(74)));
        Assert.Same(participation.Phases[1].Events[0], next);

        participation.ChangeTimeEvents([next.Id], [], DISABLED);

        Assert.IsType<FailedToQualify>(participation.Eliminated);
    }

    [Fact]
    public void The_next_phase_starts_when_the_phase_is_out_after_the_change()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var later = participation
            .UpdateSnapshot(PRESENTED, new Timestamp(PRESENT.AddMinutes(10)), ACTOR, RECORDED)
            .Event;
        Assert.Equal(OUT.AddMinutes(10), participation.Phases[1].StartTime!.ToDateTimeOffset());

        participation.ChangeTimeEvents([later.Id], [], DISABLED);

        Assert.Equal(OUT, participation.Phases[1].StartTime!.ToDateTimeOffset());

        participation.ChangeTimeEvents([], [later.Id], DISABLED.AddMinutes(1));

        Assert.Equal(OUT.AddMinutes(10), participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_change_that_leaves_the_Out_time_alone_leaves_the_start_of_the_next_phase_where_it_is()
    {
        var editedStart = OUT.AddMinutes(10);
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(editedStart, id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );
        var arrived = participation.Phases[0].Events[0].Id;
        var update = participation.UpdateSnapshot(arrived, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED).Event;

        participation.ChangeTimeEvents([update.Id], [], DISABLED);

        Assert.Equal(editedStart, participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_phase_that_completes_by_enabling_an_event_is_announced_once_and_starts_the_next_one()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        participation.ChangeTimeEvents([PRESENTED], [], DISABLED);
        participation.DequeueDomainEvents();

        participation.ChangeTimeEvents([], [PRESENTED], DISABLED.AddMinutes(1));

        var completed = Assert.Single(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(TestId.Of(1), completed.PhaseId);
    }

    [Fact]
    public void Only_the_phases_the_change_touched_are_evaluated_and_announced()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT, OUT.AddMinutes(20), OUT.AddMinutes(30), id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );
        var arrived = participation.Phases[1].Events[0].Id;
        var update = participation.UpdateSnapshot(arrived, new Timestamp(OUT.AddMinutes(22)), ACTOR, RECORDED).Event;
        participation.DequeueDomainEvents();

        participation.ChangeTimeEvents([update.Id], [], DISABLED);

        var completed = Assert.Single(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(TestId.Of(2), completed.PhaseId);
    }

    [Fact]
    public void A_phase_that_is_left_incomplete_announces_nothing_and_leaves_the_start_of_the_next_one()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        participation.DequeueDomainEvents();

        participation.ChangeTimeEvents([PRESENTED], [], DISABLED);

        Assert.Empty(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(OUT, participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_change_that_would_leave_an_earlier_phase_incomplete_while_a_later_one_has_times_is_refused_naming_the_events_and_applies_nothing()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var next = Process(participation, Arrive(OUT.AddMinutes(30), TestId.Of(74)));
        Assert.Equal(TimeEventOutcome.Accepted, next.Outcome);
        Assert.Same(participation.Phases[1].Events[0], next);

        var refused = Assert.Throws<TimelineBlockedException>(
            () => participation.ChangeTimeEvents([PRESENTED], [], DISABLED)
        );

        Assert.Equal([PRESENTED], refused.Blocking);
        Assert.Equal(TimeEventOutcome.Accepted, participation.Phases[0].Events[1].Outcome);
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Equal(OUT, participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void The_later_phase_is_asked_about_as_it_is_left_so_a_change_that_takes_its_times_too_is_allowed()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var next = Process(participation, Arrive(OUT.AddMinutes(30), TestId.Of(74)));

        participation.ChangeTimeEvents([PRESENTED, next.Id], [], DISABLED);

        Assert.Null(participation.Phases[0].PresentTime);
        Assert.Null(participation.Phases[1].ArriveTime);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    public void Any_time_of_a_later_phase_stops_the_change_that_leaves_an_earlier_phase_incomplete(
        bool arrive,
        bool present,
        bool represent
    )
    {
        var later = CreatePhase(
            OUT,
            arrive ? OUT.AddMinutes(10) : null,
            present ? OUT.AddMinutes(20) : null,
            represent: represent ? OUT.AddMinutes(30) : null,
            id: TestId.Of(2)
        );
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            later,
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );

        Assert.Throws<TimelineBlockedException>(
            () => participation.ChangeTimeEvents([participation.Phases[0].Events[1].Id], [], DISABLED)
        );
    }

    [Fact]
    public void Of_the_changes_made_to_the_phase_it_names_only_the_events_chosen_to_be_disabled()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var update = participation.UpdateSnapshot(ARRIVED, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED).Event;
        participation.ChangeTimeEvents([update.Id], [], DISABLED);
        Process(participation, Arrive(OUT.AddMinutes(30), TestId.Of(74)));

        var refused = Assert.Throws<TimelineBlockedException>(
            () => participation.ChangeTimeEvents([PRESENTED], [update.Id], DISABLED.AddMinutes(1))
        );

        Assert.Equal([PRESENTED], refused.Blocking);
        Assert.Equal(TimeEventOutcome.RejectedManually, update.Outcome); // what was to be enabled is as it was
        Assert.Equal(DISABLED, update.DisabledAt);
    }

    [Fact]
    public void Only_a_disable_in_the_phase_that_would_be_left_incomplete_is_named_and_not_one_of_another_phase()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var next = Process(participation, Arrive(OUT.AddMinutes(30), TestId.Of(74)));
        var later = participation.UpdateSnapshot(next.Id, new Timestamp(OUT.AddMinutes(33)), ACTOR, RECORDED).Event;

        var refused = Assert.Throws<TimelineBlockedException>(
            () => participation.ChangeTimeEvents([PRESENTED, later.Id], [], DISABLED)
        );

        Assert.Equal([PRESENTED], refused.Blocking); // the update of the later phase leaves it incomplete as it was, and is not why
    }

    [Fact]
    public void A_change_that_leaves_a_phase_complete_is_not_refused_whatever_the_later_phases_have()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var update = participation.UpdateSnapshot(ARRIVED, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED).Event;
        Process(participation, Arrive(OUT.AddMinutes(30), TestId.Of(74)));

        participation.ChangeTimeEvents([update.Id], [], DISABLED);

        Assert.Equal(ARRIVE, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.True(participation.Phases[0].IsComplete());
    }

    [Fact]
    public void An_elimination_that_a_person_gave_is_not_replaced_by_the_one_a_time_that_is_enabled_calls_for()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));
        var late = participation.UpdateSnapshot(PRESENTED, new Timestamp(ARRIVE.AddMinutes(45)), ACTOR, RECORDED).Event;
        participation.ChangeTimeEvents([late.Id], [], DISABLED);
        participation.Withdraw();

        participation.ChangeTimeEvents([], [late.Id], DISABLED.AddMinutes(1)); // the recovery is over the limit again

        Assert.IsType<Withdrawn>(participation.Eliminated);
    }

    [Fact]
    public void An_earlier_phase_may_be_left_incomplete_when_no_later_phase_has_a_time()
    {
        var participation = Ride(
            CreatePhase(START, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));

        participation.ChangeTimeEvents([PRESENTED], [], DISABLED);

        Assert.Null(participation.Phases[0].PresentTime);
    }

    [Fact]
    public void A_change_that_leaves_an_earlier_phase_incomplete_is_allowed_when_the_phase_was_incomplete_already()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, id: TestId.Of(1)),
            CreatePhase(OUT, OUT.AddMinutes(10), id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );

        participation.ChangeTimeEvents([participation.Phases[0].Events[0].Id], [], DISABLED);

        Assert.Null(participation.Phases[0].ArriveTime);
    }

    [Fact]
    public void Within_a_phase_no_order_of_the_times_is_asked_for()
    {
        var participation = Ride(CreatePhase(START, isFinal: true, id: TestId.Of(1)));
        Process(participation, Arrive(ARRIVE, ARRIVED));
        Process(participation, Present(PRESENT, PRESENTED));

        participation.ChangeTimeEvents([ARRIVED], [], DISABLED);

        Assert.Null(participation.Phases[0].ArriveTime);
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_time_that_the_Phase_form_cleared_is_an_empty_value_of_its_event_that_disabling_the_event_undoes()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(null, isFinal: true, id: TestId.Of(2))
        );

        participation.Update(new Form(TestId.Of(1), START, ARRIVE, null), ACTOR, RECORDED);
        var edit = participation.Phases[0].Events.OfType<PhaseUpdated>().Single();

        Assert.Null(participation.Phases[0].PresentTime);
        var cleared = Assert.Single(edit.Times);
        Assert.Equal(TimeSlot.Present, cleared.Slot);
        Assert.Null(cleared.Time);

        participation.ChangeTimeEvents([edit.Id], [], DISABLED);

        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Equal(DISABLED, edit.DisabledAt);

        participation.ChangeTimeEvents([], [edit.Id], DISABLED.AddMinutes(1));

        Assert.Null(participation.Phases[0].PresentTime);
        Assert.Null(edit.DisabledAt);
    }

    static TimeEvent Process(Participation participation, Snapshot snapshot)
    {
        return participation.Process(snapshot, ACTOR, RECORDED);
    }

    static Snapshot Arrive(DateTimeOffset time, Guid? id = null)
    {
        return new Snapshot(1, SnapshotType.Arrive, SnapshotMethod.Manual, new Timestamp(time), id);
    }

    static Snapshot Present(DateTimeOffset time, Guid? id = null)
    {
        return new Snapshot(1, SnapshotType.Present, SnapshotMethod.Manual, new Timestamp(time), id);
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
        bool isFinal = false,
        Guid? id = null,
        DateTimeOffset? represent = null,
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
        public Form(Guid id, DateTimeOffset? startTime, DateTimeOffset? arriveTime, DateTimeOffset? presentTime)
        {
            Id = id;
            StartTime = startTime;
            ArriveTime = arriveTime;
            PresentTime = presentTime;
        }

        public Guid Id { get; }
        public DateTimeOffset? StartTime { get; }
        public DateTimeOffset? ArriveTime { get; }
        public DateTimeOffset? PresentTime { get; }
        public DateTimeOffset? RepresentTime => null;
    }
}
