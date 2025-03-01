using Not.Random;
using NTS.Domain.Objects;

namespace NTS.Storage.Documents.Countries;

public class CountryDocument : Document
{
    public CountryDocument(Country country)
        : base(RandomHelper.GenerateUniqueInteger())
    {
        IsoCode = country.IsoCode;
        NfCode = country.NfCode;
        Name = country.Name;
    }

    public string IsoCode { get; init; }
    public string NfCode { get; init; }
    public string Name { get; init; }
}
