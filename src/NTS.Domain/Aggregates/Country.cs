using Not.Domain.Base;

namespace NTS.Domain.Aggregates;

public class Country : AggregateRoot
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Country(int id, string? isoCode, string? nfCode, string name) : base(id)
    {
        IsoCode = isoCode;
        NfCode = nfCode;
        Name = Required(nameof(Name), name);
    }

    public Country(string? isoCode, string? nfCode, string name) : this(GenerateId(), isoCode, nfCode, name)
    {
    }

    public string? IsoCode { get; }
    public string? NfCode { get; }
    public string Name { get; }

    public override string ToString()
    {
        return Name;
    }
}
