using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Contracts.Core.Models;

/// <summary>
/// A time event of a Phase as it is stored and sent. The kind says which of the members apply: only a presented event has
/// the marker.
/// </summary>
public class TimeEventModel
{
    public static TimeEventModel MapFrom(TimeEvent timeEvent)
    {
        return new TimeEventModel
        {
            Id = timeEvent.Id,
            Kind = timeEvent is Presented ? TimeEventKind.Presented : TimeEventKind.Arrived,
            Time = timeEvent.Time.ToDateTimeOffset(),
            IsRepresent = timeEvent is Presented { IsRepresent: true },
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
            _ => throw new NotImplementedException(),
        };
    }
}

public enum TimeEventKind
{
    Arrived = 1,
    Presented = 2,
}
