using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Application.Contracts.Shared.Models;

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
