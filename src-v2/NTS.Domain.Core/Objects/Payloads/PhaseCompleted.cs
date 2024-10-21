using NTS.Domain.Core.Entities;

namespace NTS.Domain.Core.Objects.Payloads;

public record PhaseCompleted : ParticipationEventBase
{
    public PhaseCompleted(Participation participation) : base(participation)
    {
    }
}
