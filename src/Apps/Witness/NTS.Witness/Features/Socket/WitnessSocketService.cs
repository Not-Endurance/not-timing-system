using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Features.Sessions;

namespace NTS.Witness.Features.Socket;

public class WitnessSocketService : INtsSocketService, ISingleton
{
    readonly IRpcSocket _socket;
    readonly INotifier _notifier;

    public WitnessSocketService(IRpcSocket socket, INotifier notifier)
    {
        _socket = socket;
        _notifier = notifier;
    }

    public UpcomingEvent? Principal { get; private set; }
    public UpcomingEvent? Event => Principal;

    public async Task Disconnect()
    {
        var @event = Principal;
        await _socket.Disconnect();
        if (_socket.IsConnected)
        {
            return;
        }

        Principal = null;
        await ServiceLocator.Get<IUserSessionService>().SetEventId(null);
        if (@event != null)
        {
            _notifier.Warn(string.Format(Disconnected_from__string, @event.Name));
        }
    }

    public async Task Connect(UpcomingEvent upcomingEvent)
    {
        if (Principal != null)
        {
            if (Principal.Id != upcomingEvent.Id)
            {
                _notifier.Error(
                    string.Format(Cannot_select_another_event_before_disconnect__string, Principal.Name)
                );
                return;
            }
            if (_socket.IsConnected)
            {
                return;
            }

            // Previous connection attempt may have failed; clear stale state and retry.
            Principal = null;
        }

        await _socket.Connect(upcomingEvent.Id.ToString());
        if (!_socket.IsConnected)
        {
            return;
        }

        Principal = upcomingEvent;
        await ServiceLocator.Get<IUserSessionService>().SetEventId(upcomingEvent.Id);
        if (Principal != null)
        {
            _notifier.Inform(string.Format(Connected_to__string, Principal.Name));
        }
    }
}
