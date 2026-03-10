using Not.Krud.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Application.Shared;

public class SettingModel : IDocument, IKrudModel<Setting>
{
    public static SettingModel From(Setting setting)
    {
        var model = new SettingModel();
        model.MapFrom(setting);
        return model;
    }

    public int Id { get; set; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string AccountId { get; set; } = default!;
    public CountryModel Country { get; set; } = default!;
    public DetectionMode? DetectionMode { get; set; }

    public Setting MapToEntity()
    {
        return new Setting(Country.MapToEntity(), DetectionMode, id: Id);
    }

    public void MapFrom(Setting setting)
    {
        Id = setting.Id;
        Country = CountryModel.From(setting.Country);
        DetectionMode = setting.DetectionMode;
        AccountId = setting.AccountId.ToString();
    }
}

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
