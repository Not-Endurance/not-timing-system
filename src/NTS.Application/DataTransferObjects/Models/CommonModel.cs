using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Settings;

namespace NTS.Application.DataTransferObjects.Models;
public class CommonModel
{
    public class OfficialModel : Identity
    {
        public static OfficialModel Create(Official official)
        {
            return new OfficialModel
            {
                Id = official.Id,
                Names = official.Person.Names,
                Role = official.Role,
            };
        }

        public static OfficialModel Create(Domain.Setup.Aggregates.Official official)
        {
            return new OfficialModel
            {
                Id = official.Id,
                Names = official.Person.Names,
                Role = official.Role,
            };
        }
        public string[] Names { get; init; } = [];
        public OfficialRole Role { get; init; } = default!;

        public Official ToCoreDomain()
        {
            return new Official(Id, Names, Role);
        }

        public Domain.Setup.Aggregates.Official ToSetupDomain()
        {
            return new Domain.Setup.Aggregates.Official(Id, Names, Role);
        }
    }

    public class CountryModel : Identity
    {
        public static CountryModel Create(Country country)
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

        public string Name { get; init; } = default!;
        public string? IsoCode { get; init; }
        public string? NfCode { get; init; }
        public string? Locale { get; init; }

        public Country ToDomain()
        {
            return new Country(Id, Name, IsoCode, NfCode, Locale);
        }
    }

    public class SettingModel : Identity
    {
        public static SettingModel Create(Setting setting)
        {
            return new SettingModel
            {
                Id = setting.Id,
                Country = CountryModel.Create(setting.Country),
                DetectionMode = setting.DetectionMode,
                AccountId = setting.AccountId.ToString(),
            };
        }

        public string AccountId { get; set; } = default!;
        public CountryModel Country { get; set; } = default!;
        public DetectionMode? DetectionMode { get; set; }

        public Setting ToDomain()
        {
            return new Setting(Id, Guid.Parse(AccountId), Country.ToDomain(), DetectionMode);
        }
    }
}
