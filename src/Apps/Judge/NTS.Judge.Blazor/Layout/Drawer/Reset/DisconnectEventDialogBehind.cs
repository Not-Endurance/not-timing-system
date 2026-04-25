using MudBlazor;
using Not.Blazor.Dialogs.Abstractions;
using Not.Blazor.Helpers;
using NTS.Application.Socket;
using NTS.Judge.Features.Core;

namespace NTS.Judge.Blazor.Layout.Drawer.Reset;

public class DisconnectEventDialogBehind : NDialog
{
    [Inject]
    IDashService TimingStateService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

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
