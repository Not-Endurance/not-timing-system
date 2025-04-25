using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Horses;

public class EmsHorseException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsHorse);
}
