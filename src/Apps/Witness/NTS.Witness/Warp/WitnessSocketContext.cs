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

    public UpcomingEvent? Hook { get; private set; }
    public UpcomingEvent? Event => Hook;

    public async Task Disconnect()
    {
        var @event = Hook;
        InternalSet(null);
        await _socket.Disconnect();
        if (!_socket.IsConnected && @event != null)
        {
            NotifyHelper.Warn(string.Format(Disconnected_from__string, @event.Name));
        }
    }

    public async Task Connect(UpcomingEvent upcomingEvent)
    {
        if (Hook == upcomingEvent)
        {
            return;
        }
        if (Hook != null)
        {
            NotifyHelper.Error(string.Format(Cannot_select_another_event_before_disconnect__string, Hook.Name));
            return;
        }
        InternalSet(upcomingEvent);
        await _socket.Connect();
        if (_socket.IsConnected && Hook != null)
        {
            NotifyHelper.Inform(string.Format(Connected_to__string, Hook.Name));
        }
    }

    public bool IsConnected()
    {
        return _socket.IsConnected;
    }

    void InternalSet(UpcomingEvent? upcomingEvent)
    {
        Hook = upcomingEvent;
        _metadata.ConnectionGroupKey = upcomingEvent?.Id.ToString();
    }
}
