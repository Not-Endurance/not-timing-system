using Not.Application.Behinds.Adapters;
using Not.Application.DomainEvents;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;

namespace NTS.Judge.Features.Socket;

public class JudgeSocketService : NStatefulService, INtsSocketService, IScoped
{
    readonly IRpcSocket _socket;
    readonly INotifier _notifier;
    readonly IDomainEventDispatcher _domainEventDispatcher;

    public JudgeSocketService(IRpcSocket socket, INotifier notifier, IDomainEventDispatcher domainEventDispatcher)
    {
        _socket = socket;
        _notifier = notifier;
        _domainEventDispatcher = domainEventDispatcher;
        _socket.Error += HandleRpcErrors;
        _socket.ServerConnectionChanged += HandleServerConnectionChanged;
    }

    public SocketConnectionStatus Status { get; private set; }
    public bool IsConnected => _socket?.IsConnected ?? false;
    public EventInformation? Event { get; private set; }

    public override void Dispose()
    {
        base.Dispose();
        _socket.Error -= HandleRpcErrors;
        _socket.ServerConnectionChanged -= HandleServerConnectionChanged;
    }

    public async Task Disconnect()
    {
        var currentEventId = Event?.Id;
        await _socket.Disconnect();
        await InternalSetEvent(null);
        await _domainEventDispatcher.Dispatch(new EventDisconnected(currentEventId));
    }

    public async Task Connect(EventInformation eventInformation)
    {
        if (Event == eventInformation && IsConnected)
        {
            return;
        }
        await _socket.Connect(eventInformation.Id.ToString());
        if (!_socket.IsConnected)
        {
            return;
        }
        await InternalSetEvent(eventInformation);
        await _domainEventDispatcher.Dispatch(new EventConnected(eventInformation.Id));
    }

    public Task<bool> WillResetSession(EventInformation eventInformation)
    {
        return Task.FromResult(false);
    }

    void HandleRpcErrors(object? sender, RpcError rpcError)
    {
        Status = SocketConnectionStatus.Disconnected;
        EmitChanged();
        _notifier.Error(rpcError.Exception);
    }

    void HandleServerConnectionChanged(object? sender, SocketConnectionStatus e)
    {
        var previousStatus = Status;
        Status = e;
        EmitChanged();

        if (
            e == SocketConnectionStatus.Connected
            && previousStatus == SocketConnectionStatus.Connecting
            && Event != null
        )
        {
            _ = _domainEventDispatcher.Dispatch(new EventConnected(Event.Id));
        }
    }

    Task InternalSetEvent(EventInformation? eventInformation)
    {
        Event = eventInformation;
        EmitChanged();
        return Task.CompletedTask;
    }
}
