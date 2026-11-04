using NC.Domain.Abstractions;

namespace NoTiming.Domain.Core.Events;

public record EventConnected : IDomainEvent
{
    public EventConnected(int eventId)
    {
        EventId = eventId;
    }

    public int EventId { get; }
}
