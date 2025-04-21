using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Settings;
using NTS.Storage.Documents.Countries;

namespace NTS.Storage.Documents.Settings;

public class SettingDocument : Document
{
    public static SettingDocument Create(Setting setting)
    {
        return new SettingDocument
        {
            Id = setting.Id,
            Country = CountryDocument.Create(setting.Country),
            DetectionMode = setting.DetectionMode,
            AccountId = setting.AccountId.ToString(),
        };
    }

    public string AccountId { get; set; } = default!;
    public CountryDocument Country { get; set; } = default!;
    public DetectionMode? DetectionMode { get; set; }

    public Setting ToDomain()
    {
        return new Setting(Id, Guid.Parse(AccountId), Country.ToDomain(), DetectionMode);
    }
}
