using Not.Application.RPC.SignalR;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Services;

namespace NTS.Witness.Warp;

public class WitnessSocketContext : IConnectionStatus, INtsSocketService
{
    readonly IRpcSocket _socket;

    public WitnessSocketContext(IRpcSocket socket)
    {
        _socket = socket;
    }

    public UpcomingEvent? Principal { get; private set; }
    public UpcomingEvent? Event => Principal;

    public async Task Disconnect()
    {
        var @event = Principal;
        Principal = null;
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
        Principal = upcomingEvent;
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
}
