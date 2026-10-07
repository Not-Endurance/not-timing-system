namespace NTS.Domain.Core.Aggregates.Participations.Entities;

/// <summary>
/// An Official corrected the Present time of a Snapshot they sent (ADR-0005). It is an event of its own and not another
/// <see cref="Presented"/>, because it records a different intent. It carries the marker of the presentation it changes: it
/// feeds the Present time, or the Represent time when it changes the representation of the Phase, and never makes one.
/// </summary>
public sealed class PresentUpdated : TimeEvent
{
    public PresentUpdated(
        Timestamp time,
        bool isRepresent,
        TimeEventOutcome outcome,
        SnapshotMethod method,
        DateTimeOffset? recordedAt,
        Guid? actorId,
        Guid? id = null
    )
        : base(time, outcome, method, recordedAt, actorId, id)
    {
        IsRepresent = isRepresent;
    }

    public bool IsRepresent { get; }

    public override string ToString()
    {
        var label = IsRepresent ? Represent_string : IN_string;
        return Combine($"{label}:{Time}", IsAccepted ? null : Outcome);
    }
}
