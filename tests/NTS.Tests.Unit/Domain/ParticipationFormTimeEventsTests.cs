using Not.Domain.Exceptions;
using Not.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Events;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// The Phase form of the Console (#620, ADR-0005): its Save records one event of its own, a <c>PhaseUpdated</c> (the Main
/// Operator's edit of the Phase, and not the <c>PresentUpdated</c> of an Official's Update of a Snapshot), that lists only the
/// times that differ from the ones the Phase showed, a cleared time as an explicit empty value. The Phase shows as each of its
/// times the latest accepted event that lists it, so disabling the event undoes the whole edit. Start is edited in place and
/// records nothing, a time that is out of order refuses the Save and records nothing, and the Save is evaluated like any
/// accepted change. The Phase starts at 08:00 and is shown arriving at 09:00 and presented at 09:10.
/// </summary>
public sealed class ParticipationFormTimeEventsTests
{
    static readonly DateTimeOffset START = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddHours(1);
    static readonly DateTimeOffset PRESENT = ARRIVE.AddMinutes(10);
    static readonly DateTimeOffset RECORDED = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly Guid ACTOR = TestId.Of(9);

    [Fact]
    public void Saving_one_changed_time_records_one_event_that_lists_only_that_time()
    {
        var participation = Ride();
        var arrive = ARRIVE.AddMinutes(2);

        participation.Update(new Form(TestId.Of(1), START, arrive, PRESENT, null), ACTOR, RECORDED);

        var events = participation.Phases[0].Events;
        Assert.Equal(3, events.Count);
        var updated = Assert.IsType<PhaseUpdated>(events[2]);
        var listed = Assert.Single(updated.Times);
        Assert.Equal(TimeSlot.Arrive, listed.Slot);
        Assert.Equal(arrive, listed.Time!.ToDateTimeOffset());
        Assert.Equal(TimeEventOutcome.Accepted, updated.Outcome);
        Assert.Equal(SnapshotMethod.Manual, updated.Method);
        Assert.Equal(RECORDED, updated.RecordedAt);
        Assert.Equal(ACTOR, updated.ActorId);
        Assert.NotEqual(Guid.Empty, updated.Id);
        Assert.Equal(arrive, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void Saving_several_changed_times_records_one_event_that_lists_all_of_them()
    {
        var participation = Ride(isRepresentRequested: true);
        var arrive = ARRIVE.AddMinutes(2);
        var present = PRESENT.AddMinutes(3);
        var represent = PRESENT.AddMinutes(30);

        participation.Update(new Form(TestId.Of(1), START, arrive, present, represent), ACTOR, RECORDED);

        var phase = participation.Phases[0];
        var updated = Assert.IsType<PhaseUpdated>(Assert.Single(phase.Events.Skip(2)));
        Assert.Equal(
            [(TimeSlot.Arrive, arrive), (TimeSlot.Present, present), (TimeSlot.Represent, represent)],
            updated.Times.Select(x => (x.Slot, x.Time!.ToDateTimeOffset())).ToArray()
        );
        Assert.Equal(arrive, phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(present, phase.PresentTime!.ToDateTimeOffset());
        Assert.Equal(represent, phase.RepresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void Saving_with_nothing_changed_records_nothing_and_says_so()
    {
        var participation = Ride();

        var isChanged = participation.Update(new Form(TestId.Of(1), START, ARRIVE, PRESENT, null), ACTOR, RECORDED);

        Assert.False(isChanged);
        Assert.Equal(2, participation.Phases[0].Events.Count);
        Assert.All(participation.Phases[0].Events, x => Assert.Null(x.RecordedAt));
    }

    [Fact]
    public void A_save_that_changes_something_says_so()
    {
        var participation = Ride();

        Assert.True(
            participation.Update(new Form(TestId.Of(1), START, ARRIVE.AddMinutes(2), PRESENT, null), ACTOR, RECORDED)
        );
        Assert.True(
            participation.Update(
                new Form(TestId.Of(1), START.AddMinutes(-1), ARRIVE.AddMinutes(2), PRESENT, null),
                ACTOR,
                RECORDED
            )
        );
    }

    [Fact]
    public void A_time_the_form_gives_back_as_it_showed_it_is_not_changed_though_the_time_has_a_fraction_of_a_second()
    {
        var arrive = ARRIVE.AddMilliseconds(437); // sent by the device of an Official, shown as 09:00:00
        var participation = Ride(arrive: arrive);

        var isChanged = participation.Update(new Form(TestId.Of(1), START, ARRIVE, PRESENT, null), ACTOR, RECORDED);

        Assert.False(isChanged);
        Assert.Equal(2, participation.Phases[0].Events.Count);
        Assert.Equal(arrive, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_time_left_as_it_was_shown_is_not_listed_when_another_is_changed_and_keeps_its_fraction_of_a_second()
    {
        var arrive = ARRIVE.AddMilliseconds(437);
        var participation = Ride(arrive: arrive);

        participation.Update(new Form(TestId.Of(1), START, ARRIVE, PRESENT.AddMinutes(1), null), ACTOR, RECORDED);

        var updated = Assert.IsType<PhaseUpdated>(Assert.Single(participation.Phases[0].Events.Skip(2)));
        Assert.Equal(TimeSlot.Present, Assert.Single(updated.Times).Slot);
        Assert.Equal(arrive, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_time_of_the_form_is_a_time_of_the_day_and_one_given_on_another_day_is_not_changed()
    {
        var participation = Ride(arrive: ARRIVE.AddDays(-1));

        var isChanged = participation.Update(new Form(TestId.Of(1), START, ARRIVE, PRESENT, null), ACTOR, RECORDED);

        Assert.False(isChanged);
        Assert.Equal(2, participation.Phases[0].Events.Count);
    }

    [Fact]
    public void A_time_that_is_a_second_away_is_changed()
    {
        var participation = Ride(arrive: ARRIVE.AddMilliseconds(437));

        var isChanged = participation.Update(
            new Form(TestId.Of(1), START, ARRIVE.AddSeconds(1), PRESENT, null),
            ACTOR,
            RECORDED
        );

        Assert.True(isChanged);
        var updated = Assert.IsType<PhaseUpdated>(Assert.Single(participation.Phases[0].Events.Skip(2)));
        Assert.Equal(TimeSlot.Arrive, Assert.Single(updated.Times).Slot);
    }

    [Fact]
    public void A_start_that_is_given_back_as_it_was_shown_keeps_its_fraction_of_a_second_when_another_time_is_changed()
    {
        var participation = Ride(start: START.AddMilliseconds(250));

        participation.Update(new Form(TestId.Of(1), START, ARRIVE.AddMinutes(2), PRESENT, null), ACTOR, RECORDED);

        Assert.Equal(START.AddMilliseconds(250), participation.Phases[0].StartTime!.ToDateTimeOffset());
        Assert.Equal(ARRIVE.AddMinutes(2), participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_time_the_form_clears_is_not_asked_for_its_order_so_what_follows_it_may_come_before_it_was()
    {
        var participation = Ride();
        var before = START.AddMinutes(30); // after the Start, and before the arrival that the form clears

        participation.Update(new Form(TestId.Of(1), START, null, before, null), ACTOR, RECORDED);

        Assert.Null(participation.Phases[0].ArriveTime);
        Assert.Equal(before, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_start_that_is_given_back_as_it_was_shown_is_not_changed_and_keeps_its_fraction_of_a_second()
    {
        var participation = Ride(start: START.AddMilliseconds(250));

        var isChanged = participation.Update(new Form(TestId.Of(1), START, ARRIVE, PRESENT, null), ACTOR, RECORDED);

        Assert.False(isChanged);
        Assert.Equal(START.AddMilliseconds(250), participation.Phases[0].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_time_that_is_not_there_and_is_left_empty_is_not_listed()
    {
        var participation = Ride();
        var arrive = ARRIVE.AddMinutes(2);

        participation.Update(new Form(TestId.Of(1), START, arrive, PRESENT, null), ACTOR, RECORDED);

        var updated = Assert.IsType<PhaseUpdated>(participation.Phases[0].Events[2]);
        Assert.DoesNotContain(updated.Times, x => x.Slot == TimeSlot.Represent);
        Assert.DoesNotContain(updated.Times, x => x.Slot == TimeSlot.Present);
    }

    [Fact]
    public void A_time_the_form_clears_is_listed_as_an_explicit_empty_value_and_the_phase_shows_none()
    {
        var participation = Ride();

        participation.Update(new Form(TestId.Of(1), START, ARRIVE, null, null), ACTOR, RECORDED);

        var phase = participation.Phases[0];
        Assert.Null(phase.PresentTime);
        Assert.Equal(ARRIVE, phase.ArriveTime!.ToDateTimeOffset());
        var updated = Assert.IsType<PhaseUpdated>(Assert.Single(phase.Events.Skip(2)));
        var listed = Assert.Single(updated.Times);
        Assert.Equal(TimeSlot.Present, listed.Slot);
        Assert.Null(listed.Time); // it is listed, and says there is none
        Assert.Equal(TimeEventOutcome.Accepted, phase.Events[1].Outcome); // what it hides is as it was
    }

    [Fact]
    public void Disabling_the_event_of_the_form_undoes_the_whole_edit_and_enabling_it_does_it_again()
    {
        var participation = Ride(isRepresentRequested: true);
        var arrive = ARRIVE.AddMinutes(2);
        var represent = PRESENT.AddMinutes(30);
        participation.Update(new Form(TestId.Of(1), START, arrive, null, represent), ACTOR, RECORDED);
        var edit = participation.Phases[0].Events[^1];
        var phase = participation.Phases[0];
        Assert.Null(phase.PresentTime);

        participation.ChangeTimeEvents([edit.Id], [], RECORDED.AddMinutes(5));

        Assert.Equal(ARRIVE, phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, phase.PresentTime!.ToDateTimeOffset());
        Assert.Null(phase.RepresentTime);

        participation.ChangeTimeEvents([], [edit.Id], RECORDED.AddMinutes(6));

        Assert.Equal(arrive, phase.ArriveTime!.ToDateTimeOffset());
        Assert.Null(phase.PresentTime);
        Assert.Equal(represent, phase.RepresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_later_edit_lists_what_it_changed_over_what_an_earlier_one_set()
    {
        var participation = Ride();
        participation.Update(new Form(TestId.Of(1), START, ARRIVE.AddMinutes(2), PRESENT, null), ACTOR, RECORDED);

        participation.Update(
            new Form(TestId.Of(1), START, ARRIVE.AddMinutes(2), PRESENT.AddMinutes(4), null),
            ACTOR,
            RECORDED.AddMinutes(1)
        );

        var phase = participation.Phases[0];
        var second = Assert.IsType<PhaseUpdated>(phase.Events[3]);
        var listed = Assert.Single(second.Times);
        Assert.Equal(TimeSlot.Present, listed.Slot);
        Assert.Equal(PRESENT.AddMinutes(4), phase.PresentTime!.ToDateTimeOffset());
        Assert.Equal(ARRIVE.AddMinutes(2), phase.ArriveTime!.ToDateTimeOffset());
    }

    [Fact]
    public void The_start_of_the_phase_is_edited_in_place_and_records_no_event()
    {
        var participation = Ride();
        var moved = START.AddMinutes(-10);

        participation.Update(new Form(TestId.Of(1), moved, ARRIVE, PRESENT, null), ACTOR, RECORDED);

        Assert.Equal(moved, participation.Phases[0].StartTime!.ToDateTimeOffset());
        Assert.Equal(2, participation.Phases[0].Events.Count);
    }

    [Fact]
    public void The_start_and_a_time_edited_together_are_one_event_and_the_start_in_place()
    {
        var participation = Ride();
        var moved = START.AddMinutes(-10);
        var arrive = ARRIVE.AddMinutes(2);

        participation.Update(new Form(TestId.Of(1), moved, arrive, PRESENT, null), ACTOR, RECORDED);

        Assert.Equal(moved, participation.Phases[0].StartTime!.ToDateTimeOffset());
        var updated = Assert.IsType<PhaseUpdated>(Assert.Single(participation.Phases[0].Events.Skip(2)));
        Assert.Equal(TimeSlot.Arrive, Assert.Single(updated.Times).Slot);
    }

    [Theory]
    [InlineData("arrive before start")]
    [InlineData("present before arrive")]
    [InlineData("present at arrive")]
    [InlineData("represent before present")]
    [InlineData("represent at present")]
    public void A_time_out_of_order_refuses_the_save_and_records_nothing_not_even_the_start(string mistake)
    {
        var participation = Ride(isRepresentRequested: true);
        var moved = START.AddMinutes(-10);
        var form = mistake switch
        {
            "arrive before start" => new Form(TestId.Of(1), moved, moved.AddMinutes(-1), PRESENT, null),
            "present before arrive" => new Form(TestId.Of(1), moved, ARRIVE, ARRIVE.AddMinutes(-1), null),
            "present at arrive" => new Form(TestId.Of(1), moved, ARRIVE, ARRIVE, null),
            "represent before present" => new Form(TestId.Of(1), moved, ARRIVE, PRESENT, PRESENT.AddMinutes(-1)),
            _ => new Form(TestId.Of(1), moved, ARRIVE, PRESENT, PRESENT),
        };

        var refused = Assert.Throws<DomainPropertyException>(() => participation.Update(form, ACTOR, RECORDED));

        Assert.False(string.IsNullOrWhiteSpace(refused.Property));
        var phase = participation.Phases[0];
        Assert.Equal(2, phase.Events.Count);
        Assert.Equal(START, phase.StartTime!.ToDateTimeOffset());
        Assert.Equal(ARRIVE, phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, phase.PresentTime!.ToDateTimeOffset());
        Assert.Null(phase.RepresentTime);
    }

    [Fact]
    public void The_order_is_asked_of_the_times_the_edit_leaves_and_a_phase_without_an_arrival_is_legal()
    {
        var participation = Ride();

        participation.Update(new Form(TestId.Of(1), START, null, PRESENT, null), ACTOR, RECORDED);

        Assert.Null(participation.Phases[0].ArriveTime);
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_listed_representation_feeds_the_representation_and_an_update_of_a_Snapshot_then_changes_it()
    {
        var participation = Ride(isRepresentRequested: true);
        var represent = PRESENT.AddMinutes(30);
        participation.Update(new Form(TestId.Of(1), START, ARRIVE, PRESENT, represent), ACTOR, RECORDED);
        var presented = participation.Phases[0].Events[1];

        var update = participation.UpdateSnapshot(
            presented.Id,
            new Timestamp(represent.AddMinutes(2)),
            ACTOR,
            RECORDED.AddMinutes(1)
        );

        Assert.Equal(TimeSlot.Represent, update.Slot);
        Assert.Equal(represent.AddMinutes(2), participation.Phases[0].RepresentTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_update_of_a_Snapshot_still_changes_the_representation_when_the_form_edited_only_the_presentation_after_it()
    {
        var participation = Ride(isRepresentRequested: true);
        var represent = PRESENT.AddMinutes(30);
        participation.Update(new Form(TestId.Of(1), START, ARRIVE, PRESENT, represent), ACTOR, RECORDED);
        participation.Update(
            new Form(TestId.Of(1), START, ARRIVE, PRESENT.AddMinutes(2), represent),
            ACTOR,
            RECORDED.AddMinutes(1)
        ); // the edit that follows lists the Present time alone
        var presented = participation.Phases[0].Events[1];

        var update = participation.UpdateSnapshot(
            presented.Id,
            new Timestamp(represent.AddMinutes(3)),
            ACTOR,
            RECORDED.AddMinutes(2)
        );

        Assert.Equal(TimeSlot.Represent, update.Slot); // the Official corrects the last presentation they sent
        Assert.Equal(represent.AddMinutes(3), participation.Phases[0].RepresentTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT.AddMinutes(2), participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void An_update_of_a_Snapshot_does_not_make_a_representation_that_the_form_cleared()
    {
        var participation = Ride(isRepresentRequested: true);
        var represent = PRESENT.AddMinutes(30);
        participation.Update(new Form(TestId.Of(1), START, ARRIVE, PRESENT, represent), ACTOR, RECORDED);
        participation.Update(new Form(TestId.Of(1), START, ARRIVE, PRESENT, null), ACTOR, RECORDED.AddMinutes(1));
        var presented = participation.Phases[0].Events[1];

        var update = participation.UpdateSnapshot(
            presented.Id,
            new Timestamp(PRESENT.AddMinutes(3)),
            ACTOR,
            RECORDED.AddMinutes(2)
        );

        Assert.Equal(TimeSlot.Present, update.Slot);
        Assert.Null(participation.Phases[0].RepresentTime);
        Assert.Equal(PRESENT.AddMinutes(3), participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void The_id_of_an_edit_of_the_form_is_not_the_id_of_a_Snapshot_to_update()
    {
        var participation = Ride();
        participation.Update(new Form(TestId.Of(1), START, ARRIVE.AddMinutes(2), PRESENT, null), ACTOR, RECORDED);
        var edit = participation.Phases[0].Events[^1];

        Assert.Throws<GuardException>(
            () => participation.UpdateSnapshot(edit.Id, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED)
        );
    }

    [Fact]
    public void A_phase_shows_the_edit_of_the_form_while_it_is_the_latest_that_lists_a_time_and_not_after_a_later_one_lists_them_all()
    {
        var participation = Ride();
        participation.Update(
            new Form(TestId.Of(1), START, ARRIVE.AddMinutes(2), PRESENT.AddMinutes(1), null),
            ACTOR,
            RECORDED
        );
        var first = participation.Phases[0].Events[^1];
        var phase = participation.Phases[0];
        Assert.True(phase.Shows(first));

        participation.Update(
            new Form(TestId.Of(1), START, ARRIVE.AddMinutes(4), PRESENT.AddMinutes(1), null),
            ACTOR,
            RECORDED.AddMinutes(1)
        );

        Assert.True(phase.Shows(first)); // it still gives the Present time
        participation.Update(
            new Form(TestId.Of(1), START, ARRIVE.AddMinutes(4), PRESENT.AddMinutes(5), null),
            ACTOR,
            RECORDED.AddMinutes(2)
        );
        Assert.False(phase.Shows(first)); // and now it gives none
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(15, true)]
    [InlineData(20, true)]
    public void The_recovery_the_form_leaves_decides_whether_the_required_inspection_is_compulsory(
        int recoveryMinutes,
        bool isCompulsory
    )
    {
        var participation = Ride(isFinal: false, compulsoryThreshold: TimeSpan.FromMinutes(15));

        participation.Update(
            new Form(TestId.Of(1), START, ARRIVE, ARRIVE.AddMinutes(recoveryMinutes), null),
            ACTOR,
            RECORDED
        );

        Assert.Equal(isCompulsory, participation.Phases[0].IsRequiredInspectionCompulsory);
    }

    [Fact]
    public void A_save_that_puts_the_recovery_over_the_limit_eliminates_for_time_and_disabling_the_edit_restores()
    {
        var participation = Ride(isFinal: false);

        participation.Update(new Form(TestId.Of(1), START, ARRIVE, ARRIVE.AddMinutes(50), null), ACTOR, RECORDED);

        Assert.IsType<FailedToQualify>(participation.Eliminated);

        participation.ChangeTimeEvents([participation.Phases[0].Events[^1].Id], [], RECORDED.AddMinutes(1));

        Assert.Null(participation.Eliminated);
    }

    static Participation Ride(
        bool isRepresentRequested = false,
        bool isFinal = true,
        TimeSpan? compulsoryThreshold = null,
        DateTimeOffset? start = null,
        DateTimeOffset? arrive = null
    )
    {
        const int number = 1;
        var country = new Country(TestId.Of(number), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Athlete", null, country, null, null, TestId.Of(number));
        var horse = new Horse("Horse", null, null, TestId.Of(number));
        var combination = new Combination(number, athlete, horse, null, "20", null, null, TestId.Of(number));
        var phase = new Phase(
            "",
            20,
            40,
            40,
            CompetitionRuleset.Regional,
            isFinal,
            compulsoryThreshold,
            Timestamp.Create(start ?? START),
            Timestamp.Create(arrive ?? ARRIVE),
            Timestamp.Create(PRESENT),
            null,
            isRepresentRequested,
            false,
            false,
            TestId.Of(1)
        );

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection([phase]),
            null,
            eventId: TestId.Of(1)
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
