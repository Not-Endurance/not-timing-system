using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms;
using NTS.Judge.Blazor.Setup.Settings.Components;
using NTS.Judge.Features.Setup.Settings;

namespace NTS.Judge.Blazor.Setup.Settings;

public class SettingsPageBehind : NStatefulComponent<ISettingBehind>
{
    [Inject]
    FormManager<SettingFormModel, SettingForm> FormManager { get; set; } = default!;

    protected SettingFormModel? FormModel => Service.Setting == null ? null : new SettingFormModel(Service.Setting);

    protected async Task OpenCreateForm()
    {
        await FormManager.Create();
    }
}
