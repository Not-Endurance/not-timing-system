using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Payloads;

public record RepresentationRequired : ParticipationPayload
{
    public RepresentationRequired(Participation participation)
        : base(participation) { }
}
