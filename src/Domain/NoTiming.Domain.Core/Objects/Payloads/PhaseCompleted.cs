using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Payloads;

public record PhaseCompleted : ParticipationPayload
{
    public PhaseCompleted(Participation participation)
        : base(participation) { }
}
