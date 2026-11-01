using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Domain.Core.Aggregates.Participations.Events;

/// <summary>
/// The Main Operator's edit of a Phase in the form of the Console (ADR-0005, #620): one event that lists only the times that
/// differ from the ones the Phase showed, a time that was cleared as a listed time with none. It feeds each time it lists. It is
/// an event of its own, and not an <see cref="ArriveUpdated"/> or a <see cref="PresentUpdated"/>, which are an Official's Update
/// of a Snapshot they sent: the two are named for their source and are not alike. Disabling it undoes the whole edit.
/// </summary>
public sealed class PhaseUpdated : ParticipationEventBase
{
    public PhaseUpdated(
        IEnumerable<SlotTime> times,
        TimeEventOutcome outcome,
        SnapshotMethod method,
        DateTimeOffset? recordedAt,
        Guid? actorId,
        Guid? id = null,
        DateTimeOffset? disabledAt = null
    )
        : base(outcome, method, recordedAt, actorId, id, disabledAt)
    {
        Times = [.. times];
    }

    public override IReadOnlyList<SlotTime> Times { get; }

    public override string ToString()
    {
        return Combine(
            Times.Select(x => $"{LabelOf(x.Slot)}:{(x.Time?.ToString() ?? "-")}"),
            IsAccepted ? null : Outcome
        );
    }

    static string LabelOf(TimeSlot slot)
    {
        return slot switch
        {
            TimeSlot.Arrive => ARR_string,
            TimeSlot.Represent => Represent_string,
            _ => IN_string,
        };
    }
}
