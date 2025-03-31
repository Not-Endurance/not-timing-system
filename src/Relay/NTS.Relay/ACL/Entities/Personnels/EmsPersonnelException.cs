using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Personnels;

public class EmsPersonnelException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsPersonnel);
}
