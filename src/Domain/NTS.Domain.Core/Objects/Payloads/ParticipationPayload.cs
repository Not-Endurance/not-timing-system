using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Payloads;

public abstract record ParticipationPayload
{
    public ParticipationPayload(Participation participation)
    {
        Participation = participation;
    }

    public Participation Participation { get; }
}
