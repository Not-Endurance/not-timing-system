using MudBlazor;
using Not.Blazor.Dialogs.Abstractions;
using Not.Blazor.Helpers;
using NTS.Application.Socket;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Blazor.Layout.Drawer.Reset;

public class SoftResetDialogBehind : NDialog
{
    [Inject]
    ITimingStateService TimingStateService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected async Task SoftReset()
    {
        try
        {
            await SocketService.Disconnect();
            await CloseDialog();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenHardResetDialog()
    {
        try
        {
            var hardResetDialog = await DialogService.ShowAsync<HardResetDialog>();
            if (await hardResetDialog.IsCanceled())
            {
                await CancelDialog();
                return;
            }

            await TimingStateService.Reset();
            await SocketService.Disconnect();
            await CloseDialog();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
