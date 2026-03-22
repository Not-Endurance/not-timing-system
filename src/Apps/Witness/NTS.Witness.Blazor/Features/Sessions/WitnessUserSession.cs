using Not.Application.Authentication.Abstractions;
using Not.Application.CRUD.Ports;
using NTS.Application.Socket;
using NTS.Application.UserSession;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Blazor.Features.Sessions;

public class WitnessUserSession : INUserSession
{
    readonly IUserSessionService _userSessionService;
    readonly IRepository<EnduranceEvent> _enduranceEvents;
    readonly INtsSocketService _socketService;

    public WitnessUserSession(
        IUserSessionService userSessionService,
        IRepository<EnduranceEvent> enduranceEvents,
        INtsSocketService socketService
    )
    {
        _userSessionService = userSessionService;
        _enduranceEvents = enduranceEvents;
        _socketService = socketService;
    }

    public async Task Initialize()
    {
        var session = await _userSessionService.GetCurrent();
        if (session?.EventId == null)
        {
            await DisconnectIfNeeded();
            return;
        }

        var enduranceEvent = await _enduranceEvents.Read(session.EventId.Value);
        if (enduranceEvent != null)
        {
            if (_socketService.Event != null && _socketService.Event.Id != enduranceEvent.Id)
            {
                await _socketService.Disconnect();
            }

            await _socketService.Connect(enduranceEvent);
            return;
        }

        await _userSessionService.DeleteCurrent();
        await DisconnectIfNeeded();
    }

    async Task DisconnectIfNeeded()
    {
        if (_socketService.Event == null && !_socketService.IsConnected)
        {
            return;
        }

        await _socketService.Disconnect();
    }
}
