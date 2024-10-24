using NTS.Domain.Core.Entities;

namespace NTS.Domain.Core.Objects.Payloads;

public abstract record ParticipationPayload : DomainObject
{
    public ParticipationPayload(Participation participation)
    {
        Participation = participation;
    }

    public Participation Participation { get; }
}
