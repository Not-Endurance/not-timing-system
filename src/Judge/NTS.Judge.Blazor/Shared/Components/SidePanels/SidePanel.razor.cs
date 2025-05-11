using MudBlazor;
using Not.Blazor.Dialogs;
using NTS.Judge.Blazor.Shared.Components.SidePanels.Reset;
using NTS.Judge.Blazor.Shared.Constants;

namespace NTS.Judge.Blazor.Shared.Components.SidePanels;

public partial class SidePanel
{
    [Inject]
    ICoreBehind CoreBehind { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    NavigationManager NavManager { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(CoreBehind);
    }

    public async Task Start()
    {
        await CoreBehind.Start();
    }

    async Task OpenSoftResetDialog()
    {
        var dialog = await DialogService.ShowAsync<SoftResetDialog>();
        if (await dialog.IsCanceled())
        {
            return;
        }
        NavManager.NavigateTo(BlazorPages.HOME, forceLoad: true);
    }
}
