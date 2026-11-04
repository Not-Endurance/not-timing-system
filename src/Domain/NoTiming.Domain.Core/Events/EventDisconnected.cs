using Not.Domain.Abstractions;

namespace NTS.Domain.Core.Events;

public record EventDisconnected : IDomainEvent
{
    public EventDisconnected(int? eventId)
    {
        EventId = eventId;
    }

    public int? EventId { get; }
}
