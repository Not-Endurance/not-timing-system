using Not.Async.Extensions;
using Not.Injection;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Blazor.Features.Socket;

public class EventConnectionCoordinator : IEventConnectionCoordinator, IScoped
{
    readonly IEventInformationService _eventInformationService;
    readonly INtsSocketService _socketService;
    readonly IEventConnectionDialogLauncher _dialogLauncher;

    public EventConnectionCoordinator(
        IEventInformationService eventInformationService,
        INtsSocketService socketService,
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
