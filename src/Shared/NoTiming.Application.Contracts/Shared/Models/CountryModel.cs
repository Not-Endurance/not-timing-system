using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Domain.Aggregates;

namespace NTS.Application.Contracts.Shared.Models;

public class CountryModel : IDocument, IKrudModel<Country>
{
    public static CountryModel From(Country country)
    {
        var model = new CountryModel();
        model.MapFrom(country);
        return model;
    }

    public int Id { get; set; } = default!;
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; set; } = default!;
    public string? IsoCode { get; set; }
    public string? NfCode { get; set; }
    public string? Locale { get; set; }

    public void MapFrom(Country country)
    {
        Id = country.Id;
        Name = country.Name;
        IsoCode = country.IsoCode;
        NfCode = country.NfCode;
        Locale = country.Locale;
    }

    public Country MapToEntity()
    {
        return new Country(Id, Name, IsoCode, NfCode, Locale);
    }
}
