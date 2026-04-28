using MudBlazor;
using NTS.Domain.Core.Aggregates;
using NTS.Blazor.Components.SelectEvents;
using Not.Blazor.Helpers;
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

    public async Task<bool> ConfirmSessionResetAsync(EnduranceEvent enduranceEvent)
    {
        var parameters = new DialogParameters<ChangeEventHistoryDialog>
        {
            { x => x.EventName, enduranceEvent.Name },
        };
        var dialog = await _dialogService.ShowAsync<ChangeEventHistoryDialog>(Change_event_string, parameters);
        return !await dialog.IsCanceled();
    }
}

public interface IEventConnectionDialogLauncher
{
    Task ShowSelectEventAsync();
    Task<bool> ConfirmSessionResetAsync(EnduranceEvent enduranceEvent);
}
