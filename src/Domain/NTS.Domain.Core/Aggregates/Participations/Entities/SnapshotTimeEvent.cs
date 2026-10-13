namespace NTS.Domain.Core.Aggregates.Participations.Entities;

/// <summary>
/// A time event that sets one time of a Phase: what a Snapshot records, or an Official's Update of a Snapshot (ADR-0005). It
/// is placed by its own time, and the time it feeds is told by its kind.
/// </summary>
public abstract class SnapshotTimeEvent : TimeEvent
{
    IReadOnlyList<SlotTime>? _times;

    protected SnapshotTimeEvent(
        Timestamp time,
        TimeEventOutcome outcome,
        SnapshotMethod method,
        DateTimeOffset? recordedAt,
        Guid? actorId,
        Guid? id,
        DateTimeOffset? disabledAt = null
    )
        : base(outcome, method, recordedAt, actorId, id, disabledAt)
    {
        Time = time;
    }

    /// <summary>The time of a Phase the event feeds: a Phase shows as that time the latest accepted event that does.</summary>
    public abstract TimeSlot Slot { get; }

    public Timestamp Time { get; }

    public override IReadOnlyList<SlotTime> Times => _times ??= [new SlotTime(Slot, Time)];
}
