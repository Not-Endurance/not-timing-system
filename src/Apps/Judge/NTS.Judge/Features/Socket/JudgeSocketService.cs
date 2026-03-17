using Not.Application.Behinds.Adapters;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Socket;

public class JudgeSocketService
    : NStatefulService,
        INtsSocketService,
        ISingleton
{
    readonly IRpcSocket _socket;
    readonly INotifier _notifier;

    public JudgeSocketService(
        IRpcSocket socket,
        INotifier notifier
    )
    {
        _socket = socket;
        _notifier = notifier;
        _socket.Error += HandleRpcErrors;
        _socket.ServerConnectionChanged += HandleServerConnectionChanged;
    }

    public SocketConnectionStatus Status { get; private set; }
    public bool IsConnected => _socket?.IsConnected ?? false;
    public EnduranceEvent? Event { get; private set; }

    protected override Task<bool> InitializeState()
    {
        return Task.FromResult(true);
    }

    public override void Dispose()
    {
        base.Dispose();
        _socket.Error -= HandleRpcErrors;
        _socket.ServerConnectionChanged -= HandleServerConnectionChanged;
    }

    public async Task Disconnect()
    {
        await _socket.Disconnect();
        await InternalSetEvent(null);
    }

    public async Task Connect(EnduranceEvent enduranceEvent)
    {
        if (Event == enduranceEvent)
        {
            return;
        }
        await _socket.Connect(enduranceEvent.Id.ToString());
        await InternalSetEvent(enduranceEvent);
    }

    //public async Task Handle(UpcomingEventUpdated notification, CancellationToken cancellationToken)
    //{
    //    if (Event?.Id != notification.EventId)
    //    {
    //        return;
    //    }

    //    Event = await _upcomingEvents.Read(notification.EventId);
    //    EmitChanged();
    //}

    void HandleRpcErrors(object? sender, RpcError rpcError)
    {
        Status = SocketConnectionStatus.Disconnected;
        _notifier.Error(rpcError.Exception);
    }

    void HandleServerConnectionChanged(object? sender, SocketConnectionStatus e)
    {
        Status = e;
    }

    Task InternalSetEvent(EnduranceEvent? enduranceEvent)
    {
        Event = enduranceEvent;
        EmitChanged();
        return Task.CompletedTask;
    }
}
