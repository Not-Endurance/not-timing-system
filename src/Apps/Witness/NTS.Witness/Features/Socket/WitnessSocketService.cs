using Not.Application.DomainEvents;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Domain.Core.Events;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Features.Sessions;

namespace NTS.Witness.Features.Socket;

public class WitnessSocketService : INtsSocketService, ISingleton, IDisposable
{
    readonly IRpcSocket _socket;
    readonly INotifier _notifier;
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public WitnessSocketService(
        IRpcSocket socket,
        INotifier notifier,
        IDomainEventDispatcher domainEventDispatcher
    )
    {
        _socket = socket;
        _notifier = notifier;
        _domainEventDispatcher = domainEventDispatcher;
        _socket.ServerConnectionChanged += HandleServerConnectionChanged;
    }

    public SocketConnectionStatus Status { get; private set; }
    public bool IsConnected => _socket.IsConnected;
    public UpcomingEvent? Principal { get; private set; }
    public UpcomingEvent? Event => Principal;

    public void Dispose()
    {
        _socket.ServerConnectionChanged -= HandleServerConnectionChanged;
    }

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
        await _domainEventDispatcher.Dispatch(new EventDisconnected(@event?.Id));
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
                if (_socket.IsConnected)
                {
                    _notifier.Error(
                        string.Format(Cannot_select_another_event_before_disconnect__string, Principal.Name)
                    );
                    return;
                }

                Principal = null;
            }
            else if (_socket.IsConnected)
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
        await _domainEventDispatcher.Dispatch(new EventConnected(upcomingEvent.Id));
        if (Principal != null)
        {
            _notifier.Inform(string.Format(Connected_to__string, Principal.Name));
        }
    }

    void HandleServerConnectionChanged(object? sender, SocketConnectionStatus status)
    {
        var previousStatus = Status;
        Status = status;

        if (status == SocketConnectionStatus.Connected && previousStatus == SocketConnectionStatus.Connecting && Principal != null)
        {
            _ = _domainEventDispatcher.Dispatch(new EventConnected(Principal.Id));
        }
    }
}
