using Not.Domain.Abstractions;

namespace NTS.Domain.Core.Events;

public record EventDisconnected : IDomainEvent
{
    public EventDisconnected(Guid? eventId)
    {
        EventId = eventId;
    }

    public Guid? EventId { get; }
}
