using Not.Application.Behinds.Adapters;
using Not.Injection;
using Not.Krud.Abstractions;
using NTS.Application.Settings;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Settings;

public class SettingService : NStatefulService, ISettingService, IKrudFormService<SettingFormModel>, ISingleton
{
    readonly ISettingRepository _repository;
    readonly IEnumerable<IKrudMirror<Setting>> _reflections;

    public SettingService(ISettingRepository repository, IEnumerable<IKrudMirror<Setting>> reflections)
    {
        _repository = repository;
        _reflections = reflections;
    }

    public Setting? Setting { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        Setting = await _repository.Get(Guid.Parse("ec6d8f0d-ecad-4fb6-a10f-fdb190dc0cd4"));
        return Setting != null;
    }

    public async Task Create(SettingFormModel model)
    {
        var setting = model.MapToEntity();
        await _repository.Create(setting);
        Setting = setting;
        await UpdateReflections();
    }

    public async Task Update(SettingFormModel model)
    {
        var setting = model.MapToEntity();
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
