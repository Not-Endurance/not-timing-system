using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Competitions;

public class EmsCompetitionException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsCompetition);
}
