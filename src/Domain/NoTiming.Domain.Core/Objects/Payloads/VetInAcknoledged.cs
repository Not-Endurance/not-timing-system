using Not.Domain.Abstractions;
using NTS.Domain.Core.Objects.Presentlists;

namespace NTS.Domain.Core.Objects.Payloads;

public record VetInAcknoledged : IDomainEvent
{
    VetInAcknoledged() { }

    public VetInAcknoledged(int participationNumber, int phaseId, PresentlistEntryType type, Timestamp time)
    {
        ParticipationNumber = participationNumber;
        PhaseId = phaseId;
        Type = type;
        Time = time;
    }

    public int ParticipationNumber { get; init; }
    public int PhaseId { get; init; }
    public PresentlistEntryType Type { get; init; }
    public Timestamp Time { get; init; } = Timestamp.DEFAULT;

    public PresentlistEntryKey Key => new(ParticipationNumber, PhaseId, Type);
}
