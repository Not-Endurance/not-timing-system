using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Athletes;

public class EmsAthleteException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsAthlete);
}
