using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Mud;
using NTS.Application.Socket;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Blazor.Layout.Drawer.Reset;

public partial class SoftResetDialog : NDialog
{
    [Inject]
    ITimingStateService TimingStateService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    async Task SoftReset()
    {
        await SocketService.Disconnect();
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
        await TimingStateService.Reset();
        await SocketService.Disconnect();
        Confirm();
    }
}
