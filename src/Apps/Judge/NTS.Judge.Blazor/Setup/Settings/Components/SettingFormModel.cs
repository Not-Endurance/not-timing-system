using Not.Blazor.CRUD.Forms.Ports;
using NTS.Domain.Aggregates;
using NTS.Domain.Settings;

namespace NTS.Judge.Blazor.Setup.Settings.Components;

public class SettingFormModel : IFormModel<Setting>
{
    static Country? _languageCountry;

    public SettingFormModel() { }

    public SettingFormModel(Setting entity)
    {
        FromEntity(entity);
    }

    public int? Id { get; set; }
    public Country? Country { get; set; }

    public Country? LanguageCountry
    {
        get => _languageCountry;
        set => _languageCountry = value;
    }
    public DetectionMode? DetectionMode { get; set; }

    public void FromEntity(Setting entity)
    {
        Id = entity.Id;
        Country = entity.Country;
        DetectionMode = entity.DetectionMode;
    }
}
