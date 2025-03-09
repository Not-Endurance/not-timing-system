using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Settings;

namespace NTS.Storage.Documents.Settings;

public class SettingDocument : Document
{
    public SettingDocument(Setting setting)
        : base(setting.Id)
    {
        Country = setting.Country;
        DetectionMode = setting.DetectionMode;
        AccountId = setting.AccountId.ToString();
    }

    public string AccountId { get; set; }
    public Country Country { get; set; }
    public DetectionMode DetectionMode { get; set; }

    public Setting ToDomain()
    {
        return new Setting(Id, Guid.Parse(AccountId), Country, DetectionMode);
    }
}
