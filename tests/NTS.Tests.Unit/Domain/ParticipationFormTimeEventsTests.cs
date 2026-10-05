using Not.Domain.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// The Phase form, until it records one event of its own (#620): each time it changes is recorded as an accepted time event
/// of the manual method, so that the times the Phase shows stay the latest accepted event of each slot. The Phase starts at
/// 08:00 and is shown arriving at 09:00 and presented at 09:10.
/// </summary>
public sealed class ParticipationFormTimeEventsTests
{
    static readonly DateTimeOffset START = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddHours(1);
    static readonly DateTimeOffset PRESENT = ARRIVE.AddMinutes(10);
    static readonly DateTimeOffset RECORDED = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly Guid ACTOR = TestId.Of(9);

    [Fact]
    public void Each_time_the_form_changes_is_recorded_as_an_accepted_manual_event_of_the_user_at_the_instant()
    {
        var participation = Ride(isRepresentRequested: true);
        var arrive = ARRIVE.AddMinutes(2);
        var represent = PRESENT.AddMinutes(30);

        participation.Update(new Form(TestId.Of(1), START, arrive, PRESENT, represent), ACTOR, RECORDED);

        var events = participation.Phases[0].Events;
        Assert.Equal(4, events.Count);
        var arrived = Assert.IsType<Arrived>(events[2]);
        Assert.Equal(arrive, arrived.Time.ToDateTimeOffset());
        var presented = Assert.IsType<Presented>(events[3]);
        Assert.True(presented.IsRepresent);
        Assert.Equal(represent, presented.Time.ToDateTimeOffset());
        Assert.All(
            events.Skip(2),
            x =>
            {
                Assert.Equal(TimeEventOutcome.Accepted, x.Outcome);
                Assert.Equal(SnapshotMethod.Manual, x.Method);
                Assert.Equal(RECORDED, x.RecordedAt);
                Assert.Equal(ACTOR, x.ActorId);
            }
        );
        Assert.Equal(arrive, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Equal(represent, participation.Phases[0].RepresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_time_the_form_leaves_as_it_is_records_nothing()
    {
        var participation = Ride();

        participation.Update(new Form(TestId.Of(1), START, ARRIVE, PRESENT, null), ACTOR, RECORDED);

        Assert.Equal(2, participation.Phases[0].Events.Count);
        Assert.All(participation.Phases[0].Events, x => Assert.Null(x.RecordedAt));
    }

    [Fact]
    public void A_time_the_form_clears_is_rejected_manually_and_the_phase_shows_none()
    {
        var participation = Ride();

        participation.Update(new Form(TestId.Of(1), START, ARRIVE, null, null), ACTOR, RECORDED);

        var phase = participation.Phases[0];
        Assert.Null(phase.PresentTime);
        Assert.Equal(ARRIVE, phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(2, phase.Events.Count);
        Assert.Equal(TimeEventOutcome.RejectedManually, phase.Events.OfType<Presented>().Single().Outcome);
        Assert.Equal(TimeEventOutcome.Accepted, phase.Events.OfType<Arrived>().Single().Outcome);
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
    public void Clearing_the_arrival_rejects_only_the_arrival_manually()
    {
        var participation = Ride();

        participation.Update(new Form(TestId.Of(1), START, null, PRESENT, null), ACTOR, RECORDED);

        var phase = participation.Phases[0];
        Assert.Null(phase.ArriveTime);
        Assert.Equal(PRESENT, phase.PresentTime!.ToDateTimeOffset());
        Assert.Equal(TimeEventOutcome.RejectedManually, phase.Events.OfType<Arrived>().Single().Outcome);
        Assert.Equal(TimeEventOutcome.Accepted, phase.Events.OfType<Presented>().Single().Outcome);
    }

    [Fact]
    public void Clearing_the_presentation_leaves_the_representation_accepted()
    {
        var represent = PRESENT.AddMinutes(30);
        var participation = Ride(
            isRepresentRequested: true,
            events:
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
                new Presented(
                    new Timestamp(represent),
                    true,
                    TimeEventOutcome.Accepted,
                    SnapshotMethod.Manual,
                    null,
                    null
                ),
            ]
        );

        participation.Update(new Form(TestId.Of(1), START, ARRIVE, null, represent), ACTOR, RECORDED);

        var phase = participation.Phases[0];
        Assert.Null(phase.PresentTime);
        Assert.Equal(represent, phase.RepresentTime!.ToDateTimeOffset());
        Assert.Equal(
            [TimeEventOutcome.Accepted, TimeEventOutcome.RejectedManually, TimeEventOutcome.Accepted],
            phase.Events.Select(x => x.Outcome).ToArray()
        );
    }

    [Fact]
    public void Clearing_a_time_leaves_the_reason_an_earlier_event_was_rejected_for()
    {
        var participation = Ride(
            events:
            [
                new Arrived(new Timestamp(ARRIVE), TimeEventOutcome.Accepted, SnapshotMethod.Manual, null, null),
                new Arrived(
                    new Timestamp(ARRIVE.AddMinutes(5)),
                    TimeEventOutcome.RejectedDuplicateArrive,
                    SnapshotMethod.Manual,
                    null,
                    null
                ),
            ]
        );

        participation.Update(new Form(TestId.Of(1), START, null, null, null), ACTOR, RECORDED);

        Assert.Equal(
            [TimeEventOutcome.RejectedManually, TimeEventOutcome.RejectedDuplicateArrive],
            participation.Phases[0].Events.Select(x => x.Outcome).ToArray()
        );
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
    public void The_form_refuses_an_arrival_before_the_start_and_records_nothing()
    {
        var participation = Ride();

        Assert.Throws<DomainPropertyException>(
            () =>
                participation.Update(
                    new Form(TestId.Of(1), START, START.AddMinutes(-1), PRESENT, null),
                    ACTOR,
                    RECORDED
                )
        );

        Assert.Equal(2, participation.Phases[0].Events.Count);
        Assert.Equal(ARRIVE, participation.Phases[0].ArriveTime!.ToDateTimeOffset());
    }

    static Participation Ride(
        bool isRepresentRequested = false,
        IEnumerable<TimeEvent>? events = null,
        bool isFinal = true,
        TimeSpan? compulsoryThreshold = null
    )
    {
        const int number = 1;
        var country = new Country(TestId.Of(number), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Athlete", null, country, null, null, TestId.Of(number));
        var horse = new Horse("Horse", null, null, TestId.Of(number));
        var combination = new Combination(number, athlete, horse, null, "20", null, null, TestId.Of(number));
        var phase =
            events == null
                ? new Phase(
                    "",
                    20,
                    40,
                    40,
                    CompetitionRuleset.Regional,
                    isFinal,
                    compulsoryThreshold,
                    Timestamp.Create(START),
                    Timestamp.Create(ARRIVE),
                    Timestamp.Create(PRESENT),
                    null,
                    isRepresentRequested,
                    false,
                    false,
                    TestId.Of(1)
                )
                : new Phase(
                    "",
                    20,
                    40,
                    40,
                    CompetitionRuleset.Regional,
                    isFinal,
                    compulsoryThreshold,
                    Timestamp.Create(START),
                    events,
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
