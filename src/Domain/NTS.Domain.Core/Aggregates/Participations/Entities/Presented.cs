namespace NTS.Domain.Core.Aggregates.Participations.Entities;

/// <summary>
/// A Combination was presented at the vet gate. It feeds the Present time, or the Represent time when it is marked as a
/// representation, which is taken from the Representation request of the Phase when the time is processed: a
/// re-presentation is still a presentation.
/// </summary>
public sealed class Presented : TimeEvent
{
    public Presented(
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

    public override TimeSlot Slot => IsRepresent ? TimeSlot.Represent : TimeSlot.Present;

    public override string ToString()
    {
        var label = IsRepresent ? Represent_string : IN_string;
        return Combine($"{label}:{Time}", IsAccepted ? null : Outcome);
    }
}
