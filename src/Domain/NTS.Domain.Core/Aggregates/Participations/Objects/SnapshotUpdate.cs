using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Domain.Core.Aggregates.Participations.Objects;

/// <summary>
/// What an Official's Update of a Snapshot came to (ADR-0005): the event that was recorded, accepted or rejected with its
/// reason, the Phase it was recorded in, the time of that Phase it was for, and the times that Phase showed before and after
/// it, which are the same when it was rejected.
/// </summary>
public sealed class SnapshotUpdate
{
    internal SnapshotUpdate(Phase phase, TimeEvent timeEvent, TimeSlot slot, Timestamp? previous, Timestamp? current)
    {
        Phase = phase;
        Event = timeEvent;
        Slot = slot;
        Previous = previous;
        Current = current;
    }

    /// <summary>The Phase the new time belongs to, which is where the event was recorded.</summary>
    public Phase Phase { get; }

    /// <summary>The event that was recorded.</summary>
    public TimeEvent Event { get; }

    /// <summary>The time of the Phase the Update was for.</summary>
    public TimeSlot Slot { get; }

    /// <summary>The time the Phase showed for it before, none when it showed none.</summary>
    public Timestamp? Previous { get; }

    /// <summary>The time the Phase shows for it now.</summary>
    public Timestamp? Current { get; }

    public bool IsAccepted => Event.IsAccepted;
}
