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

public class OfficialModel : IDocument
{
    //consider unifying Official in Not.Domain as a common class or interface to avoid duplication
    public static OfficialModel MapFrom(Official official)
    {
        return new OfficialModel
        {
            Id = official.Id,
            Names = official.Person.Names,
            Role = official.Role,
        };
    }

    public static OfficialModel MapFrom(Domain.Setup.Aggregates.Official official)
    {
        return new OfficialModel
        {
            Id = official.Id,
            Names = official.Person.Names,
            Role = official.Role,
        };
    }

    public int Id { get; init; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string[] Names { get; init; } = [];
    public OfficialRole Role { get; init; } = default!;

    public Official MapToCoreDomain()
    {
        return new Official(Id, Names, Role);
    }

    public Domain.Setup.Aggregates.Official MapToSetupDomain()
    {
        return new Domain.Setup.Aggregates.Official(Id, Names, Role);
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

public class AthleteModel
{
    public static AthleteModel MapFrom(IAthlete athlete)
    {
        return new AthleteModel
        {
            Id = DomainModelHelper.GenerateId(),
            FeiId = athlete.FeiId,
            Names = athlete.Names,
            Country = CountryModel.MapFrom(athlete.Country),
            Club = athlete.Club == null ? null : ClubModel.MapFrom(athlete.Club),
        };
    }

    public int Id { get; init; } = default!;
    public string[] Names { get; init; } = default!;
    public CountryModel Country { get; init; } = default!;
    public ClubModel? Club { get; init; }
    public string? FeiId { get; init; }

    public IAthlete MapToDomain()
    {
        // this guard can be removed after merge with codex/design-rpc-methods-for-nts.witness
        // when new Athlete accepts params for IAthlete only
        GuardHelper.ThrowIfDefault(Club, " cannot be null");
        var club = new Club(Club.MapToDomain());
        return new Athlete(Id, Names, Country.MapToDomain(), club, FeiId);
    }
}

public class HorseModel : IDocument
{
    public static HorseModel MapFrom(IHorse horse)
    {
        return new HorseModel
        {
            Id = horse.Id,
            FeiId = horse.FeiId,
            Name = horse.Name,
        };
    }

    public int Id { get; init; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string? FeiId { get; init; }
    public string Name { get; init; } = default!;

    public Horse MaptoDomain()
    {
        return new Horse(Id, Name, FeiId);
    }
}
