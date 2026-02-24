using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;
using Not.Startup;
using NTS.Application.Socket;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Socket;

public class JudgeSocketService
    : INtsSocketService,
        INtsSocketContext,
        IStartupInitializerAsync,
        ISingleton
{
    readonly ISocketPrincipalStorage _socketPrincialStorage;
    readonly IRpcSocket _socket;

    public JudgeSocketService(ISocketPrincipalStorage socketPrincipaStorage, IRpcSocket socket)
    {
        _socketPrincialStorage = socketPrincipaStorage;
        _socket = socket;
        _socket.Error += HandleRpcErrors;
        _socket.ServerConnectionChanged += HandleServerConnectionChanged;
    }

    public SocketConnectionStatus Status { get; private set; }
    public bool IsConnected => _socket?.IsConnected ?? false;
    public UpcomingEvent? Event { get; private set; }

    public void Dispose()
    {
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

    public async Task RunAtStartupAsync()
    {
        var upcomingEvent = await _socketPrincialStorage.Get();
        if (upcomingEvent != null)
        {
            await Connect(upcomingEvent);
        }
    }

    void HandleRpcErrors(object? sender, RpcError rpcError)
    {
        Status = SocketConnectionStatus.Disconnected;
        NotifyHelper.Error(rpcError.Exception);
    }

    void HandleServerConnectionChanged(object? sender, SocketConnectionStatus e)
    {
        Status = e;
    }

    async Task InternalSetEvent(UpcomingEvent? principal)
    {
        await _socketPrincialStorage.Commit(principal);
        Event = principal;
    }
}
