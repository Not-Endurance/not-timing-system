using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Countries;

public class EmsCountry : EmsDomainBase<EmsCountryException>
{
    [Newtonsoft.Json.JsonConstructor]
    EmsCountry() { }

    public EmsCountry(string isoCode, string name, int id)
        : base(GENERATE_ID)
    {
        Id = id;
        IsoCode = Validator.IsRequired(isoCode, nameof(isoCode));
        Name = Validator.IsRequired(name, nameof(name));
    }

    public string IsoCode { get; private set; }
    public string Name { get; private set; }
}
