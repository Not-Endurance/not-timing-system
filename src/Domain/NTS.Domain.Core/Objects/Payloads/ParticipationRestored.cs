using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Payloads;

public record ParticipationRestored : ParticipationPayload
{
    public ParticipationRestored(Participation participation)
        : base(participation) { }
}
