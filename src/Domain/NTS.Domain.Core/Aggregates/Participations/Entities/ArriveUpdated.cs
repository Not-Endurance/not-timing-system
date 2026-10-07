namespace NTS.Domain.Core.Aggregates.Participations.Entities;

/// <summary>
/// An Official corrected the Arrive time of a Snapshot they sent (ADR-0005). It is an event of its own and not another
/// <see cref="Arrived"/>, because it records a different intent, and it feeds the Arrive time like it.
/// </summary>
public sealed class ArriveUpdated : TimeEvent
{
    public ArriveUpdated(
        Timestamp time,
        TimeEventOutcome outcome,
        SnapshotMethod method,
        DateTimeOffset? recordedAt,
        Guid? actorId,
        Guid? id = null
    )
        : base(time, outcome, method, recordedAt, actorId, id) { }

    public override TimeSlot Slot => TimeSlot.Arrive;

    public override string ToString()
    {
        return Combine($"{ARR_string}:{Time}", IsAccepted ? null : Outcome);
    }
}
