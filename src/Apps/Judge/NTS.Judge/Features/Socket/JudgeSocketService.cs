using Not.Application.Behinds.Adapters;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;
using NTS.Application.Socket;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Socket;

public class JudgeSocketService : NStatefulService, INtsSocketService, INtsSocketContext, ISingleton
{
    readonly ISocketPrincipalStorage _socketPrincialStorage;
    readonly IRpcSocket _socket;
    readonly INotifier _notifier;

    public JudgeSocketService(ISocketPrincipalStorage socketPrincipaStorage, IRpcSocket socket, INotifier notifier)
    {
        _socketPrincialStorage = socketPrincipaStorage;
        _socket = socket;
        _notifier = notifier;
        _socket.Error += HandleRpcErrors;
        _socket.ServerConnectionChanged += HandleServerConnectionChanged;
    }

    public SocketConnectionStatus Status { get; private set; }
    public bool IsConnected => _socket?.IsConnected ?? false;
    public UpcomingEvent? Event { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        var upcomingEvent = await _socketPrincialStorage.Get();
        if (upcomingEvent != null)
        {
            await Connect(upcomingEvent);
        }
        return true;
    }

    public override void Dispose()
    {
        base.Dispose();
        _socket.Error -= HandleRpcErrors;
        _socket.ServerConnectionChanged -= HandleServerConnectionChanged;
    }

    public async Task Disconnect()
    {
        await InternalSetEvent(null);
        await _socket.Disconnect();
    }

    public async Task Connect(UpcomingEvent principal)
    {
        if (Event == principal)
        {
            return;
        }
        await InternalSetEvent(principal);
        await _socket.Connect(principal.Id.ToString());
    }

    void HandleRpcErrors(object? sender, RpcError rpcError)
    {
        Status = SocketConnectionStatus.Disconnected;
        _notifier.Error(rpcError.Exception);
    }

    void HandleServerConnectionChanged(object? sender, SocketConnectionStatus e)
    {
        Status = e;
    }

    async Task InternalSetEvent(UpcomingEvent? principal)
    {
        await _socketPrincialStorage.Commit(principal);
        Event = principal;
        EmitChanged();
    }
}
