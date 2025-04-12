using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Participations;

public class EmsParticipationException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsParticipation);
}
