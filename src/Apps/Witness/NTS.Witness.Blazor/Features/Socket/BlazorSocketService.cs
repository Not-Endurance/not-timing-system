using MudBlazor;
using Not.Injection;
using Not.Safe;
using NTS.Application.Socket;
using NTS.Witness.Blazor.Features.Setup;

namespace NTS.Witness.Blazor.Features.Socket;

public class BlazorSocketService : IScoped
{
    readonly IDialogService _dialogService;
    readonly INtsSocketContext _socketContext;

    public BlazorSocketService(IDialogService dialogService, INtsSocketContext socketContext)
    {
        _dialogService = dialogService;
        _socketContext = socketContext;
    }

    public async Task EnsureConnected()
    {
        try
        {
            if (_socketContext.IsConnected)
            {
                return;
            }

            var dialog = await _dialogService.ShowAsync<SelectEventDialog>(Select_event_string);
            await dialog.Result;
        }
        catch (Exception ex)
        {
            SafeHelper.HandleException(ex);
        }
    }
}
