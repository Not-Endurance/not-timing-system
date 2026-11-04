using NC.Domain.Abstractions;

namespace NoTiming.Domain.Core.Events;

public record EventDisconnected : IDomainEvent
{
    public EventDisconnected(int? eventId)
    {
        EventId = eventId;
    }

    public int? EventId { get; }
}
