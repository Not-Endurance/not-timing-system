using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.EnduranceEvents;

public class EmsEnduranceEventException : EmsDomainExceptionBase
{
    static readonly string Name = nameof(EmsEnduranceEvent);

    protected override string Entity => Name;
}
