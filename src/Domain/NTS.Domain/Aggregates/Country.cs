using Not.Domain;

namespace NTS.Domain.Aggregates;

public class Country : Aggregate
{
    public Country(Guid id, string? name, string? isoCode, string? nfCode, string? locale)
        : base(id)
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

    /// <summary>
    /// Whether the term is the name, the ISO code or the NF code of the country, in any case. It is how the profile of a
    /// person finds their country, and how an existing person is placed in a Tenant.
    /// </summary>
    public bool Matches(string? term)
    {
        return !string.IsNullOrWhiteSpace(term)
            && (
                string.Equals(Name, term, StringComparison.OrdinalIgnoreCase)
                || string.Equals(IsoCode, term, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NfCode, term, StringComparison.OrdinalIgnoreCase)
            );
    }
}
