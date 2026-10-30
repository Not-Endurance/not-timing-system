using Not.Domain.Abstractions;

namespace NTS.Domain.Core.Objects.Payloads;

/// <summary>
/// A Phase of a Participation completed (ADR-0006). It names what completed and says whether it was the last Phase; it
/// does not carry the Participation. It is raised and handled inside the server, for the Handouts: it is never sent to a
/// viewer, who is told only that a Participation changed.
/// </summary>
public record PhaseCompleted : IDomainEvent
{
    public PhaseCompleted(Guid participationId, int number, Guid phaseId, bool isFinal)
    {
        ParticipationId = participationId;
        Number = number;
        PhaseId = phaseId;
        IsFinal = isFinal;
    }

    public Guid ParticipationId { get; }
    public int Number { get; }
    public Guid PhaseId { get; }
    public bool IsFinal { get; }
}
