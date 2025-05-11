using Not.Application.CRUD.Ports;
using Not.Application.RPC.SignalR;
using Not.Domain.Exceptions;
using Not.Injection;
using Not.Startup;
using Not.Storage.Stores;
using NTS.Domain.Setup.Aggregates;
using NTS.Storage.Setup;

namespace NTS.Judge.Features.Warp;

public class EventRpcContext : IEventContext, IStartupInitializerAsync
{
    readonly IStore<SetupState> _setupStore;
    readonly IRpcSocket _socket;
    readonly WarpContext _warpContext;
    readonly IRepository<UpcomingEvent> _upcomingEvents;

    public EventRpcContext(IStore<SetupState> setupStore, IRpcSocket socket, WarpContext warpContext, IRepository<UpcomingEvent> upcomingEvents)
    {
        _setupStore = setupStore;
        _socket = socket;
        _warpContext = warpContext;
        _upcomingEvents = upcomingEvents;
    }
    
    public UpcomingEvent? Event { get; private set; }

    public async Task ResetEvent()
    {
        await InternalSetEvent(null);
        await _socket.Disconnect();
    }
    
    public async Task SetEvent(UpcomingEvent upcomingEvent)
    {
        if (Event != null && Event != upcomingEvent)
        {
            throw new DomainException(Cannot_select_another_event_without_resetting__string, Event);
        }
        await InternalSetEvent(upcomingEvent);
        await _socket.Connect();
    }
    
    public async Task RunAtStartupAsync()
    {
        var setup = await _setupStore.Readonly();
        if (setup.ConnectedEventId == null)
        {
            return;
        }
        var upcomingEvent = await _upcomingEvents.Read(setup.ConnectedEventId.Value);
        if (upcomingEvent == null)
        {
            await _setupStore.Delete();
            return;
        }
        _warpContext.Configure(Event = upcomingEvent);
        await _socket.Connect();
    }

    async Task InternalSetEvent(UpcomingEvent? upcomingEvent)
    {
        var setup = await _setupStore.Transact();
        setup.ConnectedEventId = upcomingEvent?.Id;
        await _setupStore.Commit(setup);
        _warpContext.Configure(Event = upcomingEvent);
    }
}

public interface IEventContext : ISingleton
{
    public UpcomingEvent? Event { get; }
}
