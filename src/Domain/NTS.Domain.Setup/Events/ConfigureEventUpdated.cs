using Not.Domain.Abstractions;

namespace NTS.Domain.Setup.Events;

public record ConfigureEventUpdated : IDomainEvent
{
    public ConfigureEventUpdated(Guid eventId)
    {
        EventId = eventId;
    }

    public Guid EventId { get; }
}
