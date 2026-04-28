using Not.Async.Extensions;
using Not.Injection;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Blazor.Features.Socket;

public class EventConnectionCoordinator : IEventConnectionCoordinator, IScoped
{
    readonly IEnduranceEventService _enduranceEventService;
    readonly INtsSocketService _socketService;
    readonly IEventConnectionDialogLauncher _dialogLauncher;

    public EventConnectionCoordinator(
        IEnduranceEventService enduranceEventService,
        INtsSocketService socketService,
        IEventConnectionDialogLauncher dialogLauncher
    )
    {
        _enduranceEventService = enduranceEventService;
        _socketService = socketService;
        _dialogLauncher = dialogLauncher;
    }

    public async Task EnsureConnected()
    {
        if (_socketService.IsConnected)
        {
            return;
        }

        var events = await _enduranceEventService.GetActiveEvents().ToList();
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

    async Task ConnectTo(EnduranceEvent enduranceEvent)
    {
        if (
            await _socketService.WillResetSession(enduranceEvent)
            && !await _dialogLauncher.ConfirmSessionResetAsync(enduranceEvent)
        )
        {
            return;
        }

        await _socketService.Connect(enduranceEvent);
    }
}

public interface IEventConnectionCoordinator
{
    Task EnsureConnected();
}