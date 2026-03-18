using Not.Application.Behinds.Adapters;
using Not.Application.DomainEvents;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Application.UserSession;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;

namespace NTS.Witness.Features.Socket;

public class WitnessSocketService : NStatefulService, INtsSocketService, IScoped, IDisposable
{
    readonly IUserSessionService _userSessionService;
    readonly IRpcSocket _socket;
    readonly INotifier _notifier;
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public WitnessSocketService(
        IUserSessionService userSessionService,
        IRpcSocket socket,
        INotifier notifier,
        IDomainEventDispatcher domainEventDispatcher
    )
    {
        _userSessionService = userSessionService;
        _socket = socket;
        _notifier = notifier;
        _domainEventDispatcher = domainEventDispatcher;
        _socket.ServerConnectionChanged += HandleServerConnectionChanged;
    }

    public SocketConnectionStatus Status { get; private set; }
    public bool IsConnected => _socket.IsConnected;
    public EnduranceEvent? Event { get; private set; }

    protected override Task<bool> InitializeState()
    {
        return Task.FromResult(true);
    }

    public override void Dispose()
    {
        base.Dispose();
        _socket.ServerConnectionChanged -= HandleServerConnectionChanged;
    }

    public async Task Disconnect()
    {
        var @event = Event;
        await _socket.Disconnect();
        if (_socket.IsConnected)
        {
            return;
        }

        Event = null;
        EmitChanged();
        await _domainEventDispatcher.Dispatch(new EventDisconnected(@event?.Id));
        if (@event != null)
        {
            _notifier.Warn(string.Format(Disconnected_from__string, @event.PopulatedPlace.City));
        }
    }

    public async Task Connect(EnduranceEvent upcomingEvent)
    {
        if (Event != null)
        {
            if (Event.Id != upcomingEvent.Id)
            {
                if (_socket.IsConnected)
                {
                    _notifier.Error(
                        string.Format(Cannot_select_another_event_before_disconnect__string, Event.PopulatedPlace.City)
                    );
                    return;
                }
                Event = null;
                EmitChanged();
            }
            else if (_socket.IsConnected)
            {
                return;
            }
            // Previous connection attempt may have failed; clear stale state and retry.
            Event = null;
            EmitChanged();
        }

        await _socket.Connect(upcomingEvent.Id.ToString());
        if (!_socket.IsConnected)
        {
            return;
        }

        Event = upcomingEvent;
        EmitChanged();
        await _userSessionService.SetEventId(upcomingEvent.Id);
        await _domainEventDispatcher.Dispatch(new EventConnected(upcomingEvent.Id));
        if (Event != null)
        {
            _notifier.Inform(string.Format(Connected_to__string, Event.PopulatedPlace.City));
        }
    }

    void HandleServerConnectionChanged(object? sender, SocketConnectionStatus status)
    {
        var previousStatus = Status;
        Status = status;
        EmitChanged();

        if (
            status == SocketConnectionStatus.Connected
            && previousStatus == SocketConnectionStatus.Connecting
            && Event != null
        )
        {
            _ = _domainEventDispatcher.Dispatch(new EventConnected(Event.Id));
        }
    }
}
