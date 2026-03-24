using Not.Application.Behinds.Adapters;
using Not.Application.DomainEvents;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;

namespace NTS.Judge.Features.Socket;

public class JudgeSocketService : NStatefulService, INtsSocketService, ISingleton
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
    public EnduranceEvent? Event { get; private set; }

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

    public async Task Connect(EnduranceEvent enduranceEvent)
    {
        if (Event == enduranceEvent && IsConnected)
        {
            return;
        }
        await _socket.Connect(enduranceEvent.Id.ToString());
        if (!_socket.IsConnected)
        {
            return;
        }
        await InternalSetEvent(enduranceEvent);
        await _domainEventDispatcher.Dispatch(new EventConnected(enduranceEvent.Id));
    }

    public Task<bool> WillResetSession(EnduranceEvent enduranceEvent)
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

    Task InternalSetEvent(EnduranceEvent? enduranceEvent)
    {
        Event = enduranceEvent;
        EmitChanged();
        return Task.CompletedTask;
    }
}
