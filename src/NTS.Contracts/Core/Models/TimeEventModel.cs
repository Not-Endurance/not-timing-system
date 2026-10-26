using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Contracts.Core.Models;

/// <summary>
/// A time event of a Phase as it is stored and sent. The kind says which of the members apply: an event of a Snapshot or of an
/// Update of one has its time, and only the ones that make a presentation, or change one, have the marker; an edit of the
/// Phase form has the times it lists, a time it cleared among them with none.
/// </summary>
public class TimeEventModel
{
    public static TimeEventModel MapFrom(TimeEvent timeEvent)
    {
        return new TimeEventModel
        {
            Id = timeEvent.Id,
            Kind = KindOf(timeEvent),
            Time = (timeEvent as SnapshotTimeEvent)?.Time.ToDateTimeOffset(),
            Changes = timeEvent is PhaseUpdated updated
                ? [.. updated.Times.Select(x => new SlotTimeModel { Slot = x.Slot, Time = x.Time?.ToDateTimeOffset() })]
                : null,
            IsRepresent = timeEvent is Presented { IsRepresent: true } or PresentUpdated { IsRepresent: true },
            Outcome = timeEvent.Outcome,
            Method = timeEvent.Method,
            RecordedAt = timeEvent.RecordedAt,
            ActorId = timeEvent.ActorId,
            DisabledAt = timeEvent.DisabledAt,
        };
    }

    public Guid Id { get; init; }
    public TimeEventKind Kind { get; init; }

    /// <summary>The time of an event of a Snapshot or of an Update of one; none for an edit of the Phase form, which lists its times in <see cref="Changes"/>.</summary>
    public DateTimeOffset? Time { get; init; }

    /// <summary>The times an edit of the Phase form lists: only the ones it changed, a time that it cleared with none; none for any other kind.</summary>
    public SlotTimeModel[]? Changes { get; init; }
    public bool IsRepresent { get; init; }
    public TimeEventOutcome Outcome { get; init; }
    public SnapshotMethod Method { get; init; }
    public DateTimeOffset? RecordedAt { get; init; }
    public Guid? ActorId { get; init; }

    /// <summary>When the Console Operator disabled the event; none while it is not disabled.</summary>
    public DateTimeOffset? DisabledAt { get; init; }

    public TimeEvent MapToEntity()
    {
        if (Kind == TimeEventKind.PhaseUpdated)
        {
            return new PhaseUpdated(
                (Changes ?? []).Select(x => new SlotTime(x.Slot, Timestamp.Create(x.Time))),
                Outcome,
                Method,
                RecordedAt,
                ActorId,
                Id,
                DisabledAt
            );
        }

        var time = new Timestamp(Time ?? throw new InvalidOperationException($"The time event {Id} has no time."));
        return Kind switch
        {
            TimeEventKind.Arrived => new Arrived(time, Outcome, Method, RecordedAt, ActorId, Id, DisabledAt),
            TimeEventKind.Presented => new Presented(
                time,
                IsRepresent,
                Outcome,
                Method,
                RecordedAt,
                ActorId,
                Id,
                DisabledAt
            ),
            TimeEventKind.ArriveUpdated => new ArriveUpdated(
                time,
                Outcome,
                Method,
                RecordedAt,
                ActorId,
                Id,
                DisabledAt
            ),
            TimeEventKind.PresentUpdated => new PresentUpdated(
                time,
                IsRepresent,
                Outcome,
                Method,
                RecordedAt,
                ActorId,
                Id,
                DisabledAt
            ),
            _ => throw new NotImplementedException(),
        };
    }

    static TimeEventKind KindOf(TimeEvent timeEvent)
    {
        return timeEvent switch
        {
            Arrived => TimeEventKind.Arrived,
            Presented => TimeEventKind.Presented,
            ArriveUpdated => TimeEventKind.ArriveUpdated,
            PresentUpdated => TimeEventKind.PresentUpdated,
            PhaseUpdated => TimeEventKind.PhaseUpdated,
            _ => throw new NotImplementedException(),
        };
    }
}

/// <summary>A time of a Phase that an edit of the Phase form lists, with the time it gave it, or none when it cleared it.</summary>
public class SlotTimeModel
{
    public TimeSlot Slot { get; init; }
    public DateTimeOffset? Time { get; init; }
}

public enum TimeEventKind
{
    Arrived = 1,
    Presented = 2,

    /// <summary>An Official corrected the Arrive time of a Snapshot they sent.</summary>
    ArriveUpdated = 3,

    /// <summary>An Official corrected the Present time of a Snapshot they sent, or the Represent time when the Phase has one.</summary>
    PresentUpdated = 4,

    /// <summary>The Console Operator edited the Phase in the form of the Console: one event that lists the times that were changed.</summary>
    PhaseUpdated = 5,
}
