using Not.Domain.Abstractions;

namespace NTS.Domain.Setup.Events;

public record UpcomingEventUpdated : IDomainEvent
{
    public UpcomingEventUpdated(int eventId)
    {
        EventId = eventId;
    }

    public int EventId { get; }
}
