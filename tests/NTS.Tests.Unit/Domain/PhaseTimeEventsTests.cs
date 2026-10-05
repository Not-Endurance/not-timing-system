using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// What a Phase keeps of the times it receives (#615, ADR-0005): an append-only list of time events, each with its outcome,
/// and the Arrive, Present and Represent times it shows are the latest accepted event of each slot. An Arrived event feeds
/// Arrive; a Presented event feeds Present, or Represent when it is marked as a representation.
/// </summary>
public sealed class PhaseTimeEventsTests
{
    static readonly DateTimeOffset START = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly Guid ACTOR = TestId.Of(9);

    [Fact]
    public void A_phase_without_time_events_shows_no_times()
    {
        var phase = Phase([]);

        Assert.Empty(phase.Events);
        Assert.Null(phase.ArriveTime);
        Assert.Null(phase.PresentTime);
        Assert.Null(phase.RepresentTime);
    }

    [Fact]
    public void A_time_is_the_latest_accepted_event_that_feeds_its_slot_by_the_order_the_events_were_recorded_in()
    {
        var phase = Phase(
            [
                Arrived(START.AddHours(1)),
                Presented(START.AddHours(1).AddMinutes(10)),
                Arrived(START.AddMinutes(55)),
                Presented(START.AddHours(1).AddMinutes(8)),
            ]
        );

        Assert.Equal(START.AddMinutes(55), phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(START.AddMinutes(68), phase.PresentTime!.ToDateTimeOffset());
        Assert.Null(phase.RepresentTime);
    }

    [Theory]
    [InlineData(TimeEventOutcome.RejectedDuplicateArrive)]
    [InlineData(TimeEventOutcome.RejectedDuplicatePresent)]
    [InlineData(TimeEventOutcome.RejectedParticipationComplete)]
    [InlineData(TimeEventOutcome.RejectedSeparateStageLine)]
    [InlineData(TimeEventOutcome.RejectedSeparateFinishLine)]
    [InlineData(TimeEventOutcome.RejectedInvalidTime)]
    [InlineData(TimeEventOutcome.RejectedManually)]
    public void An_event_that_was_not_accepted_feeds_no_slot_whatever_the_reason(TimeEventOutcome outcome)
    {
        var arrive = START.AddHours(1);
        var present = arrive.AddMinutes(10);
        var phase = Phase(
            [
                Arrived(arrive),
                Presented(present),
                Arrived(arrive.AddMinutes(5), outcome),
                Presented(present.AddMinutes(5), outcome),
                Presented(present.AddMinutes(30), outcome, isRepresent: true),
            ]
        );

        Assert.Equal(arrive, phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(present, phase.PresentTime!.ToDateTimeOffset());
        Assert.Null(phase.RepresentTime);
        Assert.Equal(5, phase.Events.Count);
    }

    [Fact]
    public void A_presented_event_feeds_the_presentation_or_the_representation_by_its_marker()
    {
        var present = START.AddHours(1).AddMinutes(10);
        var represent = present.AddMinutes(30);
        var later = represent.AddMinutes(5);

        var phase = Phase([Presented(present), Presented(represent, isRepresent: true), Presented(later)]);

        Assert.Equal(later, phase.PresentTime!.ToDateTimeOffset());
        Assert.Equal(represent, phase.RepresentTime!.ToDateTimeOffset());
        Assert.Null(phase.ArriveTime);
    }

    [Fact]
    public void A_phase_made_with_times_holds_an_accepted_event_of_the_manual_method_for_each_of_them()
    {
        var arrive = START.AddHours(1);
        var present = arrive.AddMinutes(10);
        var represent = present.AddMinutes(30);

        var phase = new Phase(
            "",
            20,
            40,
            40,
            CompetitionRuleset.Regional,
            false,
            null,
            Timestamp.Create(START),
            Timestamp.Create(arrive),
            Timestamp.Create(present),
            Timestamp.Create(represent),
            true,
            false,
            false
        );

        Assert.Equal(arrive, phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(present, phase.PresentTime!.ToDateTimeOffset());
        Assert.Equal(represent, phase.RepresentTime!.ToDateTimeOffset());
        Assert.Equal(3, phase.Events.Count);
        Assert.All(
            phase.Events,
            x =>
            {
                Assert.Equal(TimeEventOutcome.Accepted, x.Outcome);
                Assert.Equal(SnapshotMethod.Manual, x.Method);
                Assert.Null(x.RecordedAt);
                Assert.Null(x.ActorId);
            }
        );
        Assert.Equal(
            [typeof(Arrived), typeof(Presented), typeof(Presented)],
            phase.Events.Select(x => x.GetType()).ToArray()
        );
        Assert.Equal([false, true], phase.Events.OfType<Presented>().Select(x => x.IsRepresent).ToArray());
    }

    static Phase Phase(IEnumerable<TimeEvent> events)
    {
        return new Phase(
            "",
            20,
            40,
            40,
            CompetitionRuleset.Regional,
            false,
            null,
            Timestamp.Create(START),
            events,
            false,
            false,
            false
        );
    }

    static Arrived Arrived(DateTimeOffset time, TimeEventOutcome outcome = TimeEventOutcome.Accepted)
    {
        return new Arrived(new Timestamp(time), outcome, SnapshotMethod.Manual, START.AddHours(12), ACTOR);
    }

    static Presented Presented(
        DateTimeOffset time,
        TimeEventOutcome outcome = TimeEventOutcome.Accepted,
        bool isRepresent = false
    )
    {
        return new Presented(
            new Timestamp(time),
            isRepresent,
            outcome,
            SnapshotMethod.Manual,
            START.AddHours(12),
            ACTOR
        );
    }
}
