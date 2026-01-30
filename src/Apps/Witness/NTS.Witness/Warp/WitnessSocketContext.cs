using Not.Application.RPC.SignalR;
using Not.Notify;
using NTS.Application.SignalR;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Services;

namespace NTS.Witness.Warp;

public class WitnessSocketContext : ISelectedEventContext, IConnectionStatus, ISocketContext<UpcomingEvent>
{
    readonly IRpcSocket _socket;
    readonly WarpContext _warpContext;

    public WitnessSocketContext(IRpcSocket socket, WarpContext warpContext)
    {
        _socket = socket;
        _warpContext = warpContext;
    }

    public UpcomingEvent? Event { get; private set; }

    public UpcomingEvent? Anchor => Event;

    public async Task Disconnect()
    {
        var @event = Event;
        InternalSetEvent(null);
        await _socket.Disconnect();
        if (!_socket.IsConnected && @event != null)
        {
            NotifyHelper.Warn(string.Format(Disconnected_from__string, @event.Name));
        }
    }

    public async Task Connect(UpcomingEvent upcomingEvent)
    {
        if (Event == upcomingEvent)
        {
            return;
        }
        if (Event != null)
        {
            NotifyHelper.Error(string.Format(Cannot_select_another_event_before_disconnect__string, Event.Name));
            return;
        }
        InternalSetEvent(upcomingEvent);
        await _socket.Connect();
        if (_socket.IsConnected && Event != null)
        {
            NotifyHelper.Inform(string.Format(Connected_to__string, Event.Name));
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
