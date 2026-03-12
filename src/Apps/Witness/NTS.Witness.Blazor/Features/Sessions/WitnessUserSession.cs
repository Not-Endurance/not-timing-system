using Not.Application.Authentication.Abstractions;
using Not.Application.CRUD.Ports;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Witness.Features.Sessions;
using NTS.Witness.Features.Setup.UpcomingEvents;

namespace NTS.Witness.Blazor.Features.Sessions;

public class WitnessUserSession : INUserSession
{
    readonly IUserSessionService _userSessionService;
    readonly IUpcomingEventService _upcomingEvents;
    readonly IRepository<EnduranceEvent> _enduranceEvents;
    readonly INtsSocketService _socketService;
    readonly NavigationManager _navigationManager;
    bool _isInitialized;

    public WitnessUserSession(
        IUserSessionService userSessionService,
        IUpcomingEventService upcomingEvents,
        IRepository<EnduranceEvent> enduranceEvents,
        INtsSocketService socketService,
        NavigationManager navigationManager
    )
    {
        _userSessionService = userSessionService;
        _upcomingEvents = upcomingEvents;
        _enduranceEvents = enduranceEvents;
        _socketService = socketService;
        _navigationManager = navigationManager;
    }

    public async Task Initialize()
    {
        if (_isInitialized)
        {
            return;
        }
        _isInitialized = true;

        var session = await _userSessionService.GetCurrent();
        if (session?.EventId == null)
        {
            return;
        }

        var upcomingEvent = (await _upcomingEvents.GetEvents()).FirstOrDefault(x => x.Id == session.EventId.Value);
        if (upcomingEvent != null)
        {
            if (_socketService.Event != null && _socketService.Event.Id != upcomingEvent.Id)
            {
                await _socketService.Disconnect();
            }

            await _socketService.Connect(upcomingEvent);
            if (_socketService.Event != null)
            {
                _navigationManager.NavigateTo(Routes.SNAPSHOT_PAGE);
            }

            return;
        }

        var enduranceEvent = await _enduranceEvents.Read(0);
        if (enduranceEvent?.Id == session.EventId.Value)
        {
            return;
        }

        await _userSessionService.DeleteCurrent();
        _navigationManager.NavigateTo(Routes.HOME);
    }
}
