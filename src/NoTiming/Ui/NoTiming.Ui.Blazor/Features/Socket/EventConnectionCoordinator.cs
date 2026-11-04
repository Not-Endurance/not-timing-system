using NC.Async.Extensions;
using NC.Injection;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Ui.Blazor.Features.Socket;

public class EventConnectionCoordinator : IEventConnectionCoordinator, IScoped
{
    readonly IEventInformationService _eventInformationService;
    readonly INoTimingSocketService _socketService;
    readonly IEventConnectionDialogLauncher _dialogLauncher;

    public EventConnectionCoordinator(
        IEventInformationService eventInformationService,
        INoTimingSocketService socketService,
        IEventConnectionDialogLauncher dialogLauncher
    )
    {
        _eventInformationService = eventInformationService;
        _socketService = socketService;
        _dialogLauncher = dialogLauncher;
    }

    public async Task EnsureConnected()
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
            await ConnectTo(events[0]);
            return;
        }

        await _dialogLauncher.ShowSelectEventAsync();
    }

    async Task ConnectTo(EventInformation eventInformation)
    {
        await _socketService.Connect(eventInformation);
    }
}

public interface IEventConnectionCoordinator
{
    Task EnsureConnected();
}
