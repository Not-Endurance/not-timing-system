using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;
using Not.Startup;
using NTS.Application.SignalR;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Socket;

public class JudgeSocketContext
    : ISelectedEventContext,
        IStartupInitializerAsync,
        IGroupSocketContext<UpcomingEvent>,
        ISingleton
{
    readonly ISocketPrincipalStorage _socketPrincialStorage;
    readonly IRpcSocket _socket;
    readonly SocketMetadata _socketMetadata;

    public JudgeSocketContext(
        SocketMetadata socketMetadata,
        ISocketPrincipalStorage socketPrincipaStorage,
        IRpcSocket socket
    )
    {
        _socketMetadata = socketMetadata;
        _socketPrincialStorage = socketPrincipaStorage;
        _socket = socket;
    }

    public UpcomingEvent? Principal { get; private set; }

    public UpcomingEvent? Event => Principal;

    public async Task Disconnect()
    {
        await InternalSetEvent(null);
        await _socket.Disconnect();
    }

    public async Task Connect(UpcomingEvent upcomingEvent)
    {
        if (Principal == upcomingEvent)
        {
            return;
        }
        if (Principal != null)
        {
            NotifyHelper.Warn(string.Format(Cannot_select_another_event_without_resetting__string, Principal));
            return;
        }
        await InternalSetEvent(upcomingEvent);
        await _socket.Connect();
    }

    public async Task RunAtStartupAsync()
    {
        var upcomingEvent = await _socketPrincialStorage.Get();
        if (upcomingEvent != null)
        {
            await Connect(upcomingEvent);
        }
    }

    async Task InternalSetEvent(UpcomingEvent? upcomingEvent)
    {
        if (upcomingEvent != null)
        {
            await _socketPrincialStorage.Commit(upcomingEvent);
        }
        Principal = upcomingEvent;
        _socketMetadata.ConnectionGroupKey = Principal?.Id.ToString();
    }
}
