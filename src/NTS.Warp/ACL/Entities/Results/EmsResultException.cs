using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Results;

public class EmsResultException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsResult);
}
