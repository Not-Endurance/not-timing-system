using Not.Application.RPC.SignalR;
using Not.Domain.Exceptions;
using Not.Startup;
using NTS.Application.SignalR;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Warp;

public class JudgeSocketContext : ISelectedEventContext, IStartupInitializerAsync, ISocketContext<UpcomingEvent>
{
    readonly IConnectedEventContext _connectedEventContext;
    readonly IRpcSocket _socket;
    readonly WarpContext _warpContext;

    public JudgeSocketContext(IConnectedEventContext connectedEventContext, IRpcSocket socket, WarpContext warpContext)
    {
        _connectedEventContext = connectedEventContext;
        _socket = socket;
        _warpContext = warpContext;
    }

    public UpcomingEvent? Event { get; private set; }

    public UpcomingEvent? Anchor => Event;

    public async Task Disconnect()
    {
        await InternalSetEvent(null);
        await _socket.Disconnect();
    }

    public async Task Connect(UpcomingEvent upcomingEvent)
    {
        if (Event == upcomingEvent)
        {
            return;
        }
        if (Event != null)
        {
            throw new DomainException(Cannot_select_another_event_without_resetting__string, Event);
        }
        await InternalSetEvent(upcomingEvent);
        await _socket.Connect();
    }

    public async Task RunAtStartupAsync()
    {
        var upcomingEvent = await _connectedEventContext.Initialize();
        if (upcomingEvent == null)
        {
            return;
        }
        _warpContext.Configure(Event = upcomingEvent);
        await _socket.Connect();
    }

    async Task InternalSetEvent(UpcomingEvent? upcomingEvent)
    {
        if (upcomingEvent != null)
        {
            await _connectedEventContext.Set(upcomingEvent);
        }
        _warpContext.Configure(Event = upcomingEvent);
    }
}
