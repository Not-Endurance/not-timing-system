using MudBlazor;
using Not.Injection;
using Not.Startup;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Blazor.Components.SelectEvents;

namespace NTS.Judge.Blazor.Features.Socket;

public class JudgeStartupEnduranceEventCoordinator : IStartupInitializerAsync, IScoped
{
    readonly IEnduranceEventService _enduranceEventService;
    readonly INtsSocketService _socketService;
    readonly IJudgeSelectEventDialogLauncher _dialogLauncher;

    public JudgeStartupEnduranceEventCoordinator(
        IEnduranceEventService enduranceEventService,
        INtsSocketService socketService,
        IJudgeSelectEventDialogLauncher dialogLauncher
    )
    {
        _enduranceEventService = enduranceEventService;
        _socketService = socketService;
        _dialogLauncher = dialogLauncher;
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

        await _dialogLauncher.ShowAsync();
    }
}

public interface IJudgeSelectEventDialogLauncher
{
    Task ShowAsync();
}

public class JudgeSelectEventDialogLauncher : IJudgeSelectEventDialogLauncher, IScoped
{
    readonly IDialogService _dialogService;

    public JudgeSelectEventDialogLauncher(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task ShowAsync()
    {
        var dialog = await _dialogService.ShowAsync<SelectEventDialog>(Select_event_string);
        await dialog.Result;
    }
}
