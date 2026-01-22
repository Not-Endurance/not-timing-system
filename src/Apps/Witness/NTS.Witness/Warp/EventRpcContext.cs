using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Domain.Exceptions;
using Not.Notify;
using NTS.Application.Warp;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Services;

namespace NTS.Witness.Warp;

public class RpcContext : ISelectedEventContext, IConnectionStatus, IRpcContext<UpcomingEvent>
{
    readonly IRpcSocket _socket;
    readonly WarpContext _warpContext;

    public RpcContext(IRpcSocket socket, WarpContext warpContext)
    {
        _socket = socket;
        _warpContext = warpContext;
    }

    public UpcomingEvent? Event { get; private set; }

    public UpcomingEvent? Root => Event;

    public async Task ResetEvent()
    {
        var @event = Event;
        InternalSetEvent(null);
        await _socket.Disconnect();
        if (!_socket.IsConnected && @event != null)
        {
            NotifyHelper.Warn("Disconnected from " + @event.Name);
        }
    }

    public async Task Set(UpcomingEvent upcomingEvent)
    {
        if (Event == upcomingEvent)
        {
            return;
        }
        if (Event != null)
        {
            throw new DomainException(Cannot_select_another_event_without_resetting__string, Event);
        }
        InternalSetEvent(upcomingEvent);
        await _socket.Connect();
        if (_socket.IsConnected && Event != null)
        {
            NotifyHelper.Inform("Connected to " + Event.Name);
        }
    }

    public bool IsConnected()
    {
        return _socket.IsConnected;
    }

    void InternalSetEvent(UpcomingEvent? upcomingEvent)
    {
        _warpContext.Configure(Event = upcomingEvent);
    }
}
