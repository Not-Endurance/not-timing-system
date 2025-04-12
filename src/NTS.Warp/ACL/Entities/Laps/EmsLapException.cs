using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Laps;

public class EmsLapException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsLap);
}
