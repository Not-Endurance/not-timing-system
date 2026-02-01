using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Mud;
using NTS.Judge.Features.Core;

namespace NTS.Judge.Blazor.Shared.Components.SidePanels.Reset;

public partial class SoftResetDialog : NDialog
{
    [Inject]
    ICoreService CoreService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    async Task SoftReset()
    {
        await CoreService.SoftReset();
        Confirm();
    }

    async Task OpenHardResetDialog()
    {
        var hardResetDialog = await DialogService.ShowAsync<HardResetDialog>();
        if (await hardResetDialog.IsCanceled())
        {
            Cancel();
            return;
        }
        await CoreService.HardReset();
        Confirm();
    }
}
