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
        Guid? id
    )
        : base(id)
    {
        Time = time;
        Outcome = outcome;
        Method = method;
        RecordedAt = recordedAt;
        ActorId = actorId;
    }

    public Timestamp Time { get; }
    public TimeEventOutcome Outcome { get; private set; }
    public SnapshotMethod Method { get; }

    /// <summary>The instant the server recorded the event, null for a time that was there before events were kept.</summary>
    public DateTimeOffset? RecordedAt { get; }

    /// <summary>The signed-in user that recorded the event, null for a time that was there before events were kept.</summary>
    public Guid? ActorId { get; }

    public bool IsAccepted => Outcome == TimeEventOutcome.Accepted;

    internal void RejectManually()
    {
        Outcome = TimeEventOutcome.RejectedManually;
    }
}
