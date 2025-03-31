using NTS.Domain.Aggregates;

namespace NTS.Storage.Documents.Countries;

public class CountryDocument : Document
{
    public static CountryDocument Create(Country country)
    {
        return new CountryDocument
        {
            Id = country.Id,
            Name = country.Name,
            IsoCode = country.IsoCode,
            NfCode = country.NfCode,
            Locale = country.Locale,
        };
    }

    public string Name { get; init; } = default!;
    public string? IsoCode { get; init; }
    public string? NfCode { get; init; }
    public string? Locale { get; init; }

    public Country ToDomain()
    {
        return new Country(Id, Name, IsoCode, NfCode, Locale);
    }
}
