using Not.Domain.Abstractions;

namespace NTS.Domain.Setup.Objects.Payloads;

public record UpcomingEventUpdatedEvent : IDomainEvent
{
    public UpcomingEventUpdatedEvent(int eventId)
    {
        EventId = eventId;
    }

    public int EventId { get; }
}
