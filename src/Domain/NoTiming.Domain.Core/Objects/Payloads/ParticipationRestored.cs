using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Domain.Core.Objects.Payloads;

public record ParticipationRestored : ParticipationPayload
{
    public ParticipationRestored(Participation participation)
        : base(participation) { }
}
