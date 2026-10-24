using Not.Domain.Abstractions;

namespace NTS.Domain.Core.Events;

/// <summary>
/// The one thing the server tells a viewer about a Participation (ADR-0006, ADR-0013): that it changed. It names the
/// Participation and never describes it; the viewer reads it again, where authorization applies.
/// </summary>
public record ParticipationChanged : IDomainEvent
{
    public ParticipationChanged(Guid eventId, Guid participationId)
    {
        EventId = eventId;
        ParticipationId = participationId;
    }

    public Guid EventId { get; }
    public Guid ParticipationId { get; }
}
