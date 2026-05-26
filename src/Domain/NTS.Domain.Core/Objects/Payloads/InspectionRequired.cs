using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Payloads;

public record InspectionRequired : ParticipationPayload
{
    public InspectionRequired(Participation participation)
        : base(participation) { }
}
