using NTS.Domain.Core.Entities;

namespace NTS.Domain.Core.Objects.Payloads;

public record ParticipationRestored : ParticipationEventBase
{
    public ParticipationRestored(Participation participation) : base(participation)
    {
    }
}
