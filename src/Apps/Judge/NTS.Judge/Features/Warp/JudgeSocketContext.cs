using Not.Application.RPC.SignalR;
using Not.Domain.Exceptions;
using Not.Startup;
using NTS.Application.SignalR;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Warp;

public class JudgeSocketContext : ISelectedEventContext, IStartupInitializerAsync, IGroupSocketContext<UpcomingEvent>
{
    readonly ISocketConnectionHookStorage _socketConnectionHookStorage;
    readonly IRpcSocket _socket;

    public JudgeSocketContext(ISocketConnectionHookStorage socketConnectionHookStorage, IRpcSocket socket)
    {
        _socketConnectionHookStorage = socketConnectionHookStorage;
        _socket = socket;
    }

    public UpcomingEvent? Hook { get; private set; }

    public UpcomingEvent? Event => Hook;

    public string? ConnectionGroupKey => throw new NotImplementedException();

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
            throw new DomainException(Cannot_select_another_event_without_resetting__string, Hook);
        }
        await InternalSetEvent(upcomingEvent);
        await _socket.Connect();
    }

    public async Task RunAtStartupAsync()
    {
        var hook = await _socketConnectionHookStorage.GetConnectionHook();
        if (hook == null)
        {
            return;
        }
        Hook = hook;
        await _socket.Connect();
    }

    async Task InternalSetEvent(UpcomingEvent? upcomingEvent)
    {
        if (upcomingEvent != null)
        {
            await _socketConnectionHookStorage.CommitConnectionHook(upcomingEvent);
        }
        Hook = upcomingEvent;
    }
}
