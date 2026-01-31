using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Domain.Exceptions;
using Not.Injection;
using Not.Notify;
using Not.Startup;
using NTS.Application.SignalR;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Warp;

public class JudgeSocketContext : ISelectedEventContext, IStartupInitializerAsync, IGroupSocketContext<UpcomingEvent>, ISingleton
{
    readonly ISocketConnectionHookStorage _socketConnectionHookStorage;
    readonly IRpcSocket _socket;
    SocketMetadata _eventConnectionSocketMetadata;

    public JudgeSocketContext(
        SocketMetadata eventConnectionSocketMetadata,
        ISocketConnectionHookStorage socketConnectionHookStorage,
        IRpcSocket socket)
    {
        _eventConnectionSocketMetadata = eventConnectionSocketMetadata;
        _socketConnectionHookStorage = socketConnectionHookStorage;
        _socket = socket;
    }

    public UpcomingEvent? Hook { get; private set; }

    public UpcomingEvent? Event => Hook;

    public async Task Disconnect()
    {
        await InternalSetEvent(null);
        await _socket.Disconnect();
    }

    public async Task Connect(UpcomingEvent upcomingEvent)
    {
        if (Hook == upcomingEvent)
        {
            return;
        }
        if (Hook != null)
        {
            NotifyHelper.Warn(string.Format(Cannot_select_another_event_without_resetting__string, Hook));
            return;
        }
        await InternalSetEvent(upcomingEvent);
        await _socket.Connect();
    }

    public async Task RunAtStartupAsync()
    {
        var upcomingEvent = await _socketConnectionHookStorage.GetConnectionHook();
        if (upcomingEvent != null)
        {
            await Connect(upcomingEvent);
        }
    }

    async Task InternalSetEvent(UpcomingEvent? upcomingEvent)
    {
        if (upcomingEvent != null)
        {
            await _socketConnectionHookStorage.CommitConnectionHook(upcomingEvent);
        }
        Hook = upcomingEvent;
        _eventConnectionSocketMetadata.ConnectionGroupKey = Hook?.Id.ToString();
    }
}
