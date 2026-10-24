using Not.Domain.Abstractions;

namespace NTS.Domain.Core.Events;

public record EventConnected : IDomainEvent
{
    public EventConnected(Guid eventId)
    {
        EventId = eventId;
    }

    public Guid EventId { get; }
}
