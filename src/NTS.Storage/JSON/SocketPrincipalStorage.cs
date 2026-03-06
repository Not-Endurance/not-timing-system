using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Filesystem;
using Not.Storage.JsonFile.Stores.Files;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Socket;
using NTS.Storage.Setup;

namespace NTS.Storage.JSON;

public class SocketPrincipalStorage : LockingJsonFileStore<SetupState>, ISocketPrincipalStorage
{
    readonly IRepository<UpcomingEvent> _upcomingEvents;

    public SocketPrincipalStorage(
        [FromKeyedServices("NDataKey")] IFilesystemContext configuration,
        IRepository<UpcomingEvent> upcomingEvents
    )
        : base(configuration)
    {
        _upcomingEvents = upcomingEvents;
    }

    public async Task<UpcomingEvent?> Get()
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

    public async Task Commit(UpcomingEvent? upcomingEvent)
    {
        var setup = await Transact();
        setup.ConnectedEventId = upcomingEvent?.Id;
        await Commit(setup);
    }
}
