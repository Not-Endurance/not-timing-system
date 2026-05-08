using MudBlazor;
using NTS.Blazor.Components.SelectEvents;
using Not.Injection;

namespace NTS.Witness.Blazor.Features.Socket;

public class EventConnectionDialogLauncher : IEventConnectionDialogLauncher, IScoped
{
    readonly IDialogService _dialogService;

    public EventConnectionDialogLauncher(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task ShowSelectEventAsync()
    {
        var dialog = await _dialogService.ShowAsync<SelectEventDialog>(Select_event_string);
        await dialog.Result;
    }

}

public interface IEventConnectionDialogLauncher
{
    Task ShowSelectEventAsync();
}
