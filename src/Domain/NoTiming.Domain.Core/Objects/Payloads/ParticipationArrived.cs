using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Domain.Core.Objects.Payloads;

public record ParticipationArrived : ParticipationPayload
{
    public ParticipationArrived(Participation participation)
        : base(participation) { }
}
