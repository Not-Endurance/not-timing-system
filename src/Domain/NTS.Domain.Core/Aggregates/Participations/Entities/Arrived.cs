namespace NTS.Domain.Core.Aggregates.Participations.Entities;

/// <summary>A Combination arrived at the end of a Phase's loop. It feeds the Arrive time.</summary>
public sealed class Arrived : TimeEvent
{
    public Arrived(
        Timestamp time,
        TimeEventOutcome outcome,
        SnapshotMethod method,
        DateTimeOffset? recordedAt,
        Guid? actorId,
        Guid? id = null
    )
        : base(time, outcome, method, recordedAt, actorId, id) { }

    public override string ToString()
    {
        return Combine($"{ARR_string}:{Time}", IsAccepted ? null : Outcome);
    }
}
