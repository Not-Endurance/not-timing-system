using Not.Domain.Abstractions;

namespace NTS.Domain.Setup.Events;

public record ConfigureEventUpdated : IDomainEvent
{
    public ConfigureEventUpdated(int eventId)
    {
        EventId = eventId;
    }

    public int EventId { get; }
}
