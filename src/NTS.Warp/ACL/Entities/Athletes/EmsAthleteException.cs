using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Athletes;

public class EmsAthleteException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsAthlete);
}
