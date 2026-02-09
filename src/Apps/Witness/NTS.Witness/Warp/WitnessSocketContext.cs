using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Notify;
using NTS.Application.SignalR;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Services;

namespace NTS.Witness.Warp;

public class WitnessSocketContext : ISelectedEventContext, IConnectionStatus, IGroupSocketContext<UpcomingEvent>
{
    readonly IRpcSocket _socket;
    readonly SocketMetadata _metadata;

    public WitnessSocketContext(IRpcSocket socket, SocketMetadata metadata)
    {
        _socket = socket;
        _metadata = metadata;
    }

    public UpcomingEvent? Principal { get; private set; }
    public UpcomingEvent? Event => Principal;

    public async Task Disconnect()
    {
        var @event = Principal;
        InternalSet(null);
        await _socket.Disconnect();
        if (!_socket.IsConnected && @event != null)
        {
            NotifyHelper.Warn(string.Format(Disconnected_from__string, @event.Name));
        }
    }

    public async Task Connect(UpcomingEvent upcomingEvent)
    {
        if (Principal == upcomingEvent)
        {
            return;
        }
        if (Principal != null)
        {
            NotifyHelper.Error(string.Format(Cannot_select_another_event_before_disconnect__string, Principal.Name));
            return;
        }
        InternalSet(upcomingEvent);
        await _socket.Connect();
        if (_socket.IsConnected && Principal != null)
        {
            NotifyHelper.Inform(string.Format(Connected_to__string, Principal.Name));
        }
    }

    public bool IsConnected()
    {
        return _socket.IsConnected;
    }

    void InternalSet(UpcomingEvent? upcomingEvent)
    {
        Principal = upcomingEvent;
        _metadata.ConnectionGroupKey = upcomingEvent?.Id.ToString();
    }
}
