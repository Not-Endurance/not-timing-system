using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Results;

public class EmsResultException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsResult);
}
