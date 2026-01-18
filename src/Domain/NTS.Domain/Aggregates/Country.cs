using Not.Domain.Aggregates;

namespace NTS.Domain.Aggregates;

public class Country : Aggregate
{
    public Country(int id, string? name, string? isoCode, string? nfCode, string? locale)
        : base(id)
    {
        Name = name ?? "Default Name";
        IsoCode = isoCode ?? "ISO";
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
