using Not.Krud.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Judge.Contracts.Features.Settings;

public record SettingFormModel : KrudFormModel<Setting>
{
    public SettingFormModel() { }

    public SettingFormModel(Setting setting)
    {
        MapFrom(setting);
    }

    public SettingFormModel(KrudFormModel<Setting> original)
        : base(original) { }

    public Country? Country { get; set; }
    public Country? LanguageCountry { get; set; }
    public DetectionMode? DetectionMode { get; set; }

    protected override Setting MapTo()
    {
        return new Setting(Country, DetectionMode);
    }

    public override void MapFrom(Setting entity)
    {
        Id = entity.Id;
        Country = entity.Country;
        DetectionMode = entity.DetectionMode;
    }
}
