using NC.Domain.Abstractions;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Domain.Core.Objects.Payloads;

public abstract record ParticipationPayload : IDomainEvent
{
    public ParticipationPayload(Participation participation)
    {
        Participation = participation;
    }

    public Participation Participation { get; }
}
