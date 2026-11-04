using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Payloads;

public record ParticipationArrived : ParticipationPayload
{
    public ParticipationArrived(Participation participation)
        : base(participation) { }
}
