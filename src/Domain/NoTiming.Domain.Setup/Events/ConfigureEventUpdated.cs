using NC.Domain.Abstractions;

namespace NoTiming.Domain.Setup.Events;

public record ConfigureEventUpdated : IDomainEvent
{
    public ConfigureEventUpdated(int eventId)
    {
        EventId = eventId;
    }

    public int EventId { get; }
}
