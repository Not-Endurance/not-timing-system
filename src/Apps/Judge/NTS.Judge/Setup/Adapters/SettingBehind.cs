using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Settings;
using NTS.Judge.Blazor.Setup.Settings;
using NTS.Judge.Blazor.Setup.Settings.Components;
using NTS.Judge.HTTP;

namespace NTS.Judge.Setup.Adapters;

public class SettingBehind : ObservableBehind, ISettingBehind
{
    readonly ISettingRepository _repository;
    readonly IEnumerable<ICrudReflection<Setting>> _reflections;
    readonly IAccountBehind _accountBehind;

    public SettingBehind(
        ISettingRepository repository,
        IEnumerable<ICrudReflection<Setting>> reflections,
        IAccountBehind accountBehind
    )
    {
        _repository = repository;
        _reflections = reflections;
        _accountBehind = accountBehind;
    }

    public Setting? Setting { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        Setting = await _repository.Get(_accountBehind.Id);
        return Setting != null;
    }

    public async Task Create(SettingFormModel model)
    {
        var setting = new Setting(_accountBehind.Id, model.Country, model.DetectionMode);
        await _repository.Create(setting);
        Setting = setting;
        await UpdateReflections();
    }

    public async Task Update(SettingFormModel model)
    {
        var setting = new Setting(model.Id, _accountBehind.Id, model.Country, model.DetectionMode);
        await _repository.Update(setting);
        await UpdateReflections();
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
