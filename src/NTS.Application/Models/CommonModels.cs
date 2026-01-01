using Not.Exceptions;
using Not.Extensions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;
using NTS.Domain.Settings;

namespace NTS.Application.Models;

public class SettingModel : IDocument
{
    public static SettingModel MapFrom(Setting setting)
    {
        return new SettingModel
        {
            Id = setting.Id,
            Country = CountryModel.MapFrom(setting.Country),
            DetectionMode = setting.DetectionMode,
            AccountId = setting.AccountId.ToString(),
        };
    }

    public int Id { get; init; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string AccountId { get; set; } = default!;
    public CountryModel Country { get; set; } = default!;
    public DetectionMode? DetectionMode { get; set; }

    public Setting ToDomain()
    {
        return new Setting(Id, Guid.Parse(AccountId), Country.MapToDomain(), DetectionMode);
    }
}

public class CountryModel : IDocument
{
    public static CountryModel MapFrom(Country country)
    {
        return new CountryModel
        {
            Id = country.Id,
            Name = country.Name,
            IsoCode = country.IsoCode,
            NfCode = country.NfCode,
            Locale = country.Locale,
        };
    }

    public int Id { get; init; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; init; } = default!;
    public string? IsoCode { get; init; }
    public string? NfCode { get; init; }
    public string? Locale { get; init; }

    public Country MapToDomain()
    {
        return new Country(Id, Name, IsoCode, NfCode, Locale);
    }
}

public class ClubModel : IDocument
{
    public static ClubModel MapFrom(IClub club)
    {
        return new ClubModel { Id = club.Id, Name = club.Name };
    }

    public int Id { get; init; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; init; } = default!;

    public IClub MapToDomain()
    {
        return new Club(Id, Name);
    }
}
