using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Domain.Exceptions;
using NTS.Application.Warp;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Warp;

public class RpcContext : ISelectedEventContext, IRpcContext<UpcomingEvent>
{
    readonly IRpcSocket _socket;
    readonly WarpContext _warpContext;

    public RpcContext(IRpcSocket socket, WarpContext warpContext)
    {
        _socket = socket;
        _warpContext = warpContext;
    }

    public UpcomingEvent? Event { get; private set; }

    public UpcomingEvent? Root => Event;

    public async Task ResetEvent()
    {
        InternalSetEvent(null);
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
        InternalSetEvent(upcomingEvent);
        await _socket.Connect();
    }

    void InternalSetEvent(UpcomingEvent? upcomingEvent)
    {
        _warpContext.Configure(Event = upcomingEvent);
    }
}
