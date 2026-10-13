namespace NTS.Domain.Core.Aggregates.Participations.Entities;

/// <summary>
/// A time that reached a Phase, kept whatever became of it (ADR-0005): the time, how it was acquired, who recorded it and
/// when, and whether it was accepted or why it was not. A Phase only adds events, and shows as each of its times the latest
/// accepted event that feeds it.
/// </summary>
public abstract class TimeEvent : Entity
{
    protected TimeEvent(
        Timestamp time,
        TimeEventOutcome outcome,
        SnapshotMethod method,
        DateTimeOffset? recordedAt,
        Guid? actorId,
        Guid? id,
        DateTimeOffset? disabledAt = null
    )
        : base(id)
    {
        Time = time;
        Outcome = outcome;
        Method = method;
        RecordedAt = recordedAt;
        ActorId = actorId;
        DisabledAt = disabledAt;
    }

    /// <summary>The time of a Phase the event feeds: a Phase shows as that time the latest accepted event that does.</summary>
    public abstract TimeSlot Slot { get; }

    public Timestamp Time { get; }
    public TimeEventOutcome Outcome { get; private set; }
    public SnapshotMethod Method { get; }

    /// <summary>The instant the server recorded the event, null for a time that was there before events were kept.</summary>
    public DateTimeOffset? RecordedAt { get; }

    /// <summary>The signed-in user that recorded the event, null for a time that was there before events were kept.</summary>
    public Guid? ActorId { get; }

    /// <summary>The instant the Main Operator disabled the event (ADR-0005), null while it is not disabled.</summary>
    public DateTimeOffset? DisabledAt { get; private set; }

    public bool IsAccepted => Outcome == TimeEventOutcome.Accepted;

    /// <summary>Whether the Main Operator may disable the event: only one that was accepted.</summary>
    public bool CanBeDisabled => IsAccepted;

    /// <summary>Whether the Main Operator may enable the event: only one that the Main Operator disabled, and none the rules rejected.</summary>
    public bool CanBeEnabled => Outcome == TimeEventOutcome.RejectedManually;

    internal void Disable(DateTimeOffset at)
    {
        Outcome = TimeEventOutcome.RejectedManually;
        DisabledAt = at;
    }

    internal void Enable()
    {
        Outcome = TimeEventOutcome.Accepted;
        DisabledAt = null;
    }

    /// <summary>Puts the event back as it was, which a change that was refused after it was made does.</summary>
    internal void Restore(TimeEventOutcome outcome, DateTimeOffset? disabledAt)
    {
        Outcome = outcome;
        DisabledAt = disabledAt;
    }
}
