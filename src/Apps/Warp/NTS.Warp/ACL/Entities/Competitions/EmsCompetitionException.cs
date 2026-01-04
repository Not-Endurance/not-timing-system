using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Competitions;

public class EmsCompetitionException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsCompetition);
}
