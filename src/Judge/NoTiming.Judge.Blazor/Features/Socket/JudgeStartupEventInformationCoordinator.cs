using MudBlazor;
using Not.Async.Extensions;
using Not.Injection;
using Not.Startup;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Blazor.Components.SelectEvents;

namespace NTS.Judge.Blazor.Features.Socket;

public class JudgeStartupEventInformationCoordinator : IStartupInitializerAsync, IScoped
{
    readonly IEventInformationService _eventInformationService;
    readonly INtsSocketService _socketService;
    readonly IJudgeSelectEventDialogLauncher _dialogLauncher;

    public JudgeStartupEventInformationCoordinator(
        IEventInformationService eventInformationService,
        INtsSocketService socketService,
        IJudgeSelectEventDialogLauncher dialogLauncher
    )
    {
        _eventInformationService = eventInformationService;
        _socketService = socketService;
        _dialogLauncher = dialogLauncher;
    }

    public async Task RunAtStartupAsync()
    {
        if (_socketService.IsConnected)
        {
            return;
        }

        var events = await _eventInformationService.GetActive().ToList();
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
