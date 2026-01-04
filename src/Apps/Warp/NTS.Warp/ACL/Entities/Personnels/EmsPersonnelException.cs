using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Personnels;

public class EmsPersonnelException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsPersonnel);
}
