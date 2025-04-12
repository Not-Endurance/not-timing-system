using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Participations;

public class EmsParticipationException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsParticipation);
}
