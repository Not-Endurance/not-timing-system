using NC.Domain;

namespace NoTiming.Domain.Aggregates;

public class Country : Aggregate
{
    public Country(int id, string? name, string? isoCode, string? nfCode, string? locale, int? storedDomainVersion = null)
        : base(id, storedDomainVersion: storedDomainVersion)
    {
        Name = Required(nameof(Name), name);
        IsoCode = Required(nameof(IsoCode), isoCode);
        NfCode = nfCode;
        Locale = locale;
    }

    public string IsoCode { get; }
    public string Name { get; }
    public string? NfCode { get; }
    public string? Locale { get; }

    public override string ToString()
    {
        return Name;
    }
}
