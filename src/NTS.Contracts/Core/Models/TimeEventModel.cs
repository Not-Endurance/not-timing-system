using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Contracts.Core.Models;

/// <summary>
/// A time event of a Phase as it is stored and sent. The kind says which of the members apply: only the events that make a
/// presentation, or change one, have the marker.
/// </summary>
public class TimeEventModel
{
    public static TimeEventModel MapFrom(TimeEvent timeEvent)
    {
        return new TimeEventModel
        {
            Id = timeEvent.Id,
            Kind = KindOf(timeEvent),
            Time = timeEvent.Time.ToDateTimeOffset(),
            IsRepresent = timeEvent is Presented { IsRepresent: true } or PresentUpdated { IsRepresent: true },
            Outcome = timeEvent.Outcome,
            Method = timeEvent.Method,
            RecordedAt = timeEvent.RecordedAt,
            ActorId = timeEvent.ActorId,
        };
    }

    public Guid Id { get; init; }
    public TimeEventKind Kind { get; init; }
    public DateTimeOffset Time { get; init; }
    public bool IsRepresent { get; init; }
    public TimeEventOutcome Outcome { get; init; }
    public SnapshotMethod Method { get; init; }
    public DateTimeOffset? RecordedAt { get; init; }
    public Guid? ActorId { get; init; }

    public TimeEvent MapToEntity()
    {
        var time = new Timestamp(Time);
        return Kind switch
        {
            TimeEventKind.Arrived => new Arrived(time, Outcome, Method, RecordedAt, ActorId, Id),
            TimeEventKind.Presented => new Presented(time, IsRepresent, Outcome, Method, RecordedAt, ActorId, Id),
            TimeEventKind.ArriveUpdated => new ArriveUpdated(time, Outcome, Method, RecordedAt, ActorId, Id),
            TimeEventKind.PresentUpdated => new PresentUpdated(
                time,
                IsRepresent,
                Outcome,
                Method,
                RecordedAt,
                ActorId,
                Id
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
            _ => throw new NotImplementedException(),
        };
    }
}

public enum TimeEventKind
{
    Arrived = 1,
    Presented = 2,

    /// <summary>An Official corrected the Arrive time of a Snapshot they sent.</summary>
    ArriveUpdated = 3,

    /// <summary>An Official corrected the Present time of a Snapshot they sent, or the Represent time when the Phase has one.</summary>
    PresentUpdated = 4,
}
