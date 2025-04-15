using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Countries;

public class EmsCountryException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsCountry);
}
