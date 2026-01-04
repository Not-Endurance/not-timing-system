using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Payloads;

public record ParticipationEliminated : ParticipationPayload
{
    public ParticipationEliminated(Participation participation)
        : base(participation) { }
}
