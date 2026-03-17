using MudBlazor;
using Not.Injection;
using Not.Startup;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Blazor.Components.SelectEvents;

namespace NTS.Judge.Blazor.Features.Socket;

public class JudgeStartupEnduranceEventCoordinator : IStartupInitializerAsync, IScoped
{
    readonly IEnduranceEventService _enduranceEventService;
    readonly INtsSocketService _socketService;
    readonly IDialogService _dialogService;

    public JudgeStartupEnduranceEventCoordinator(
        IEnduranceEventService enduranceEventService,
        INtsSocketService socketService,
        IDialogService dialogService
    )
    {
        _enduranceEventService = enduranceEventService;
        _socketService = socketService;
        _dialogService = dialogService;
    }

    public async Task RunAtStartupAsync()
    {
        if (_socketService.IsConnected)
        {
            return;
        }

        var events = (await _enduranceEventService.GetEvents()).ToList();
        if (events.Count == 0)
        {
            return;
        }

        if (events.Count == 1)
        {
            await _socketService.Connect(events[0]);
            return;
        }

        var dialog = await _dialogService.ShowAsync<SelectEventDialog>(Select_event_string);
        await dialog.Result;
    }
}
