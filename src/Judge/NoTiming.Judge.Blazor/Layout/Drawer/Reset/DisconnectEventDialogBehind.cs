using MudBlazor;
using NC.Blazor.Dialogs.Abstractions;
using NC.Blazor.Helpers;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Judge.Contracts.Features.Core;

namespace NoTiming.Judge.Blazor.Layout.Drawer.Reset;

public class DisconnectEventDialogBehind : NDialog
{
    [Inject]
    IDashService TimingStateService { get; set; } = default!;

    [Inject]
    INoTimingSocketService SocketService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected async Task Disconnect()
    {
        try
        {
            await SocketService.Disconnect();
            await ConfirmDialog();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenResetTimingDialog()
    {
        try
        {
            var hardResetDialog = await DialogService.ShowAsync<ResetEventDialog>();
            if (await hardResetDialog.IsCanceled())
            {
                await CancelDialog();
                return;
            }

            await TimingStateService.Reset();
            await SocketService.Disconnect();
            await ConfirmDialog();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
