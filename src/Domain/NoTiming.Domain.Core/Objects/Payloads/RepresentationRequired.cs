using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Domain.Core.Objects.Payloads;

public record RepresentationRequired : ParticipationPayload
{
    public RepresentationRequired(Participation participation)
        : base(participation) { }
}
