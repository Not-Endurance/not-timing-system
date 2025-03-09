using Not.Domain.Base;

namespace NTS.Domain.Aggregates;

public class Country : AggregateRoot, IAggregateRoot
{
    public Country(int id, string? isoCode, string? nfCode, string name) : base(id)
    {
        Name = Required(nameof(Name), name);
        IsoCode = Required(nameof(IsoCode), isoCode);
        NfCode = nfCode;
    }

    public string IsoCode { get; }
    public string? NfCode { get; }
    public string Name { get; }

    public override string ToString()
    {
        return Name;
    }
}
