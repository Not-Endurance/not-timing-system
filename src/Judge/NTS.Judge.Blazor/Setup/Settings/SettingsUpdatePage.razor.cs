using Not.Blazor.CRUD.Forms;
using NTS.Judge.Blazor.Setup.Settings.Components;

namespace NTS.Judge.Blazor.Setup.Settings;

public partial class SettingsUpdatePage
{
    [Inject]
    ISettingBehind Behind { get; set; } = default!;

    [Inject]
    FormManager<SettingFormModel, SettingForm> FormManager { get; set; } = default!;

    protected SettingFormModel? FormModel => Behind.Setting == null
        ? null
        : new SettingFormModel(Behind.Setting);

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind);
    }

    async Task OpenCreateForm()
    {
        await FormManager.Create();
    }
}
