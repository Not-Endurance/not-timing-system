using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Horses;

public class EmsHorseException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsHorse);
}
