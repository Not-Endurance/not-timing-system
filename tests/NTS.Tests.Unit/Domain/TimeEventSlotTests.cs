using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// The time of a Phase an event feeds (ADR-0005): a Phase shows as each of its times the latest accepted event that feeds it,
/// so which time that is belongs to the event, an Update of a Snapshot included.
/// </summary>
public sealed class TimeEventSlotTests
{
    static readonly Timestamp TIME = new(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void An_arrival_and_an_update_of_one_feed_the_Arrive_time()
    {
        Assert.Equal(
            TimeSlot.Arrive,
            new Arrived(TIME, TimeEventOutcome.Accepted, SnapshotMethod.Manual, null, null).Slot
        );
        Assert.Equal(
            TimeSlot.Arrive,
            new ArriveUpdated(TIME, TimeEventOutcome.Accepted, SnapshotMethod.Manual, null, null).Slot
        );
    }

    [Theory]
    [InlineData(false, TimeSlot.Present)]
    [InlineData(true, TimeSlot.Represent)]
    public void A_presentation_and_an_update_of_one_feed_the_Present_time_or_the_Represent_time_by_their_marker(
        bool isRepresent,
        TimeSlot slot
    )
    {
        var presented = new Presented(TIME, isRepresent, TimeEventOutcome.Accepted, SnapshotMethod.Manual, null, null);
        var updated = new PresentUpdated(
            TIME,
            isRepresent,
            TimeEventOutcome.Accepted,
            SnapshotMethod.Manual,
            null,
            null
        );

        Assert.Equal(slot, presented.Slot);
        Assert.Equal(slot, updated.Slot);
    }
}
