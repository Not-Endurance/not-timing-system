using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Countries;

public class EmsCountryException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsCountry);
}
