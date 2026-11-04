using Not.Application.Behinds.Adapters;
using Not.Application.DomainEvents;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Injection;
using NTS.Application.Contracts.Socket;
using NTS.Application.UserSession;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;

namespace NTS.Witness.Features.Socket;

public class WitnessSocketService : NStatefulService, INtsSocketService, IScoped, IDisposable
{
    readonly IWitnessUserSession _userSessionService;
    readonly IRpcSocket _socket;
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public WitnessSocketService(
        IWitnessUserSession userSessionService,
        IRpcSocket socket,
        IDomainEventDispatcher domainEventDispatcher
    )
    {
        _userSessionService = userSessionService;
        _socket = socket;
        _domainEventDispatcher = domainEventDispatcher;
        _socket.ServerConnectionChanged += HandleServerConnectionChanged;
    }

    public SocketConnectionStatus Status { get; private set; }
    public bool IsConnected => _socket.IsConnected;
    public EventInformation? Event { get; private set; }

    protected override Task<bool> InitializeState()
    {
        return Task.FromResult(true);
    }

    public override void Dispose()
    {
        base.Dispose();
        _socket.ServerConnectionChanged -= HandleServerConnectionChanged;
    }

    public async Task<bool> WillResetSession(EventInformation eventInformation)
    {
        await Task.CompletedTask;
        return false;
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
    }

    public async Task Connect(EventInformation configureEvent)
    {
        if (Event?.Id != configureEvent.Id && _socket.IsConnected)
        {
            await _socket.Disconnect();
            Event = null;
        }

        await _socket.Connect(configureEvent.Id.ToString());
        if (!_socket.IsConnected)
        {
            return;
        }

        Event = configureEvent;
        EmitChanged();
        await _userSessionService.SetEventId(configureEvent.Id);
        await _domainEventDispatcher.Dispatch(new EventConnected(configureEvent.Id));
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
