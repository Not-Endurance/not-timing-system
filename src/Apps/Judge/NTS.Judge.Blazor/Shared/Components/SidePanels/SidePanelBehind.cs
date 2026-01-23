using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Dialogs;
using NTS.Judge.Blazor.Shared.Components.SidePanels.Reset;
using NTS.Judge.Blazor.Shared.Constants;
using NTS.Judge.Features.Core;

namespace NTS.Judge.Blazor.Shared.Components.SidePanels;

public class SidePanelBehind : NStatefulComponent<ICoreService>
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    NavigationManager NavManager { get; set; } = default!;

    protected async Task Start()
    {
        await Service.Start();
    }

    protected async Task OpenSoftResetDialog()
    {
        var dialog = await DialogService.ShowAsync<SoftResetDialog>();
        if (await dialog.IsCanceled())
        {
            return;
        }
        NavManager.NavigateTo(BlazorPages.HOME, forceLoad: true);
    }
}
