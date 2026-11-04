using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Domain.Core.Objects.Payloads;

public record PhaseCompleted : ParticipationPayload
{
    public PhaseCompleted(Participation participation)
        : base(participation) { }
}
