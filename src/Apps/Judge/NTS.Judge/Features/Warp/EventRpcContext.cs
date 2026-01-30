using Not.Application.Krud.Abstractions;
using Not.Application.RPC.SignalR;
using Not.Domain;
using Not.Domain.Exceptions;
using Not.Injection;
using Not.Startup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Warp;

// TODO: fix RpcContext reset not ressetting correctly
public class EventRpcContext : ISelectedEventContext, IStartupInitializerAsync, IRpcContext<UpcomingEvent>
{
    readonly IConnectedEventContext _connectedEventContext;
    readonly IRpcSocket _socket;
    readonly WarpContext _warpContext;

    public EventRpcContext(IConnectedEventContext connectedEventContext, IRpcSocket socket, WarpContext warpContext)
    {
        _connectedEventContext = connectedEventContext;
        _socket = socket;
        _warpContext = warpContext;
    }

    public UpcomingEvent? Event { get; private set; }

    public UpcomingEvent? Root => Event;

    public async Task ResetEvent()
    {
        await InternalSetEvent(null);
        await _socket.Disconnect();
    }

    public async Task Set(UpcomingEvent upcomingEvent)
    {
        if (Event == upcomingEvent)
        {
            return;
        }
        if (Event != null)
        {
            throw new DomainException(Cannot_select_another_event_without_resetting__string, Event);
        }
        await InternalSetEvent(upcomingEvent);
        await _socket.Connect();
    }

    public async Task RunAtStartupAsync()
    {
        var upcomingEvent = await _connectedEventContext.Initialize();
        if (upcomingEvent == null)
        {
            return;
        }
        _warpContext.Configure(Event = upcomingEvent);
        await _socket.Connect();
    }

    async Task InternalSetEvent(UpcomingEvent? upcomingEvent)
    {
        if (upcomingEvent == null)
        {
            return;
        }
        await _connectedEventContext.Set(upcomingEvent);
        _warpContext.Configure(Event = upcomingEvent);
    }
}

public interface ISelectedEventContext : ISingleton
{
    public UpcomingEvent? Event { get; }
}

public interface IRpcContext<T> : ISingleton
    where T : Aggregate
{
    T? Root { get; }
    Task Set(T root);
}
