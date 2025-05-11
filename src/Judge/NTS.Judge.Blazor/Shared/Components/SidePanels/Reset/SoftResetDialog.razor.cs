using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Dialogs;

namespace NTS.Judge.Blazor.Shared.Components.SidePanels.Reset;

public partial class SoftResetDialog : NDialog
{
    [Inject]
    ICoreBehind CoreBehind { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    async Task SoftReset()
    {
        await CoreBehind.SoftReset();
        Confirm();
    }

    async Task OpenHardResetDialog()
    {
        var hardResetDialog = await DialogService.ShowAsync<HardResetDialog>();
        if (await hardResetDialog.IsCanceled())
        {
            Cancel();
        }
        await CoreBehind.HardReset();
        Confirm();
    }
}
