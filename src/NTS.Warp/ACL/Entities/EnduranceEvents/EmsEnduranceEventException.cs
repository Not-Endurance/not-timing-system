using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.EnduranceEvents;

public class EmsEnduranceEventException : EmsDomainExceptionBase
{
    static readonly string Name = nameof(EmsEnduranceEvent);

    protected override string Entity => Name;
}
