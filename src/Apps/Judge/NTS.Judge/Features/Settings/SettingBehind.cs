using Microsoft.AspNetCore.SignalR;
using Not.Application.Behinds.Adapters;
using Not.Injection;
using Not.Krud.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Setup.Services;

namespace NTS.Judge.Features.Settings;

public class SettingBehind : NStatefulService, ISettingBehind, ISingleton
{
    readonly ISettingRepository _repository;
    readonly IEnumerable<IKrudMirror<Setting>> _reflections;
    readonly IAccountBehind _accountBehind;

    public SettingBehind(
        ISettingRepository repository,
        IEnumerable<IKrudMirror<Setting>> reflections,
        IAccountBehind accountBehind
    )
    {
        _repository = repository;
        _reflections = reflections;
        _accountBehind = accountBehind;
    }

    public Setting? Setting { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        Setting = await _repository.Get(_accountBehind.Id);
        return Setting != null;
    }

    public async Task Create(SettingFormModel model)
    {
        var setting = CreateSetting(model);
        await _repository.Create(setting);
        Setting = setting;
        await UpdateReflections();
    }

    public async Task Update(SettingFormModel model)
    {
        var setting = CreateSetting(model);
        await _repository.Update(setting);
        await UpdateReflections();
    }

    Setting CreateSetting(SettingFormModel model)
    {
        return new Setting(_accountBehind.Id, model.Country, model.DetectionMode, id: model.Id);
    }

    async Task UpdateReflections()
    {
        if (Setting != null)
        {
            foreach (var reflection in _reflections)
            {
                await reflection.Reflect(Setting);
            }
        }
    }
}
