using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Domain.Core.Objects.Payloads;

public record InspectionRequired : ParticipationPayload
{
    public InspectionRequired(Participation participation)
        : base(participation) { }
}
