using Not.Blazor.Components;
using Not.Blazor.Dialogs;
using NTS.Judge.Blazor.Setup.Settings.Components;
using NTS.Judge.Features.Setup.Settings;

namespace NTS.Judge.Blazor.Setup.Settings;

public class SettingsPageBehind : NStatefulComponent<ISettingBehind>
{
    [Inject]
    FormDialogService<SettingFormModel, SettingForm> DialogService { get; set; } = default!;

    protected SettingFormModel? FormModel => Service.Setting == null ? null : new SettingFormModel(Service.Setting);

    protected async Task OpenCreateForm()
    {
        try
        {
            await DialogService.ShowCreateForm();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
