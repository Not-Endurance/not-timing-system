using Not.Random;
using NTS.Domain.Aggregates;

namespace NTS.Storage.Documents.Countries;

public class CountryDocument : Document
{
    public static CountryDocument Create(Country country)
    {
        return new CountryDocument
        {
            Id = country.Id,
            IsoCode = country.IsoCode,
            NfCode = country.NfCode,
            Name = country.Name,
        };
    }

    public string? IsoCode { get; init; }

    public string? NfCode { get; init; }

    public string Name { get; init; } = default!;

    public Country ToDomain()
    {
        return new Country(Id, IsoCode, NfCode, Name);
    }
}
