using Not.Application.Behinds.Adapters;
using Not.Application.Krud.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Setup.Services;

namespace NTS.Judge.Features.Setup.Settings;

public class SettingBehind : NStatefulService, ISettingBehind
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

    protected override async Task<bool> CreateState(params IEnumerable<object> arguments)
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
