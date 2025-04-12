using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Laps;

public class EmsLapException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsLap);
}
