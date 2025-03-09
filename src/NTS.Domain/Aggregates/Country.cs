using Not.Domain.Base;

namespace NTS.Domain.Aggregates;

public class Country : AggregateRoot, IAggregateRoot
{
    public Country(int id, string? isoCode, string? nfCode, string? name) : base(id)
    {
        Name = name ?? "Default Name";
        IsoCode = isoCode ?? "ISO";
        NfCode = nfCode;
    }

    public string IsoCode { get; }
    public string Name { get; }
    public string? NfCode { get; }

    public override string ToString()
    {
        return Name;
    }
}
