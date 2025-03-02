using Not.Domain.Base;

namespace NTS.Domain.Aggregates;

public class Country : AggregateRoot
{
    public Country(string isoCode, string nfCode, string name) : base(GenerateId())
    {
        IsoCode = isoCode;
        NfCode = nfCode;
        Name = name;
    }

    public string IsoCode { get; }
    public string NfCode { get; }
    public string Name { get; }

    public override string ToString()
    {
        return Name;
    }
}
