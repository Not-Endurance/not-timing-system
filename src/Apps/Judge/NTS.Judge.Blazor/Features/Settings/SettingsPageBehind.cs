using Not.Blazor.Components.Abstractions;
using Not.Krud.Blazor;
using NTS.Judge.Contracts.Features.Settings;

namespace NTS.Judge.Blazor.Features.Settings;

public class SettingsPageBehind : NStatefulComponent
{
    [Inject]
    ISettingService SettingsService { get; set; } = default!;

    [Inject]
    KrudDialogService<SettingFormModel, SettingShell> KrudService { get; set; } = default!;

    protected SettingFormModel? Model { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(SettingsService);
    }

    protected override void OnBeforeRender()
    {
        if (SettingsService.Setting != null)
        {
            Model = new SettingFormModel(SettingsService.Setting);
        }
    }

    protected async Task OpenCreateForm()
    {
        try
        {
            await KrudService.ShowCreateForm();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
