using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Filesystem;
using Not.Storage.JsonFile.Stores.Files;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;
using NTS.Storage.Setup;

namespace NTS.Storage.JSON;

public class SelectedEventStore : LockingJsonFileStore<SetupState>, IConnectedEventContext
{
    readonly IRepository<UpcomingEvent> _upcomingEvents;

    public SelectedEventStore([FromKeyedServices("NDataKey")] IFileContext configuration, IRepository<UpcomingEvent> upcomingEvents) : base(configuration)
    {
        _upcomingEvents = upcomingEvents;
    }

    public async Task<UpcomingEvent?> Initialize()
    {
        var setup = await Readonly();
        if (setup.ConnectedEventId == null)
        {
            return null;
        }
        var upcomingEvent = await _upcomingEvents.Read(setup.ConnectedEventId.Value);
        if (upcomingEvent == null)
        {
            await Delete();
            return null;
        }
        return upcomingEvent;
    }

    public async Task Set(UpcomingEvent upcomingEvent)
    {
        var setup = await Transact();
        setup.ConnectedEventId = upcomingEvent?.Id;
        await Commit(setup);
    }
}
