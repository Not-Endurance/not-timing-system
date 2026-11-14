using NC.Krud.Abstractions;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Enums;

namespace NoTiming.Application.Contracts.Shared.Models;

public class SettingModel : IDocument, IKrudModel<Setting>
{
    public static SettingModel From(Setting setting)
    {
        var model = new SettingModel();
        model.MapFrom(setting);
        return model;
    }

    public int DomainVersion { get; set; }

    public int Id { get; set; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string AccountId { get; set; } = default!;
    public CountryModel Country { get; set; } = default!;
    public DetectionMode? DetectionMode { get; set; }

    public Setting MapToEntity()
    {
        return new Setting(Country.MapToEntity(), DetectionMode, id: Id, storedDomainVersion: DomainVersion);
    }

    public void MapFrom(Setting setting)
    {
        DomainVersion = setting.DomainVersion;
        Id = setting.Id;
        Country = CountryModel.From(setting.Country);
        DetectionMode = setting.DetectionMode;
        AccountId = setting.AccountId.ToString();
    }
}
