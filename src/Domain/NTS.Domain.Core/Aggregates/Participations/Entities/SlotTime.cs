namespace NTS.Domain.Core.Aggregates.Participations.Entities;

/// <summary>A time of a Phase that an event lists, and what the event says of it: the time it gives, or none when it clears it (ADR-0005).</summary>
public sealed class SlotTime
{
    public SlotTime(TimeSlot slot, Timestamp? time)
    {
        Slot = slot;
        Time = time;
    }

    public TimeSlot Slot { get; }

    /// <summary>The time the event gives it; none when the event says there is none.</summary>
    public Timestamp? Time { get; }
}
