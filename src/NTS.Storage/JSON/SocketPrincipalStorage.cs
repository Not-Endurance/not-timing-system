using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Filesystem;
using Not.Storage.JsonFile.Stores.Files;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.Setup;

namespace NTS.Storage.JSON;

public class SocketPrincipalStorage : LockingJsonFileStore<SetupState>, ISocketPrincipalStorage
{
    readonly IRepository<EnduranceEvent> _upcomingEvents;

    public SocketPrincipalStorage(
        [FromKeyedServices("NDataKey")] IFilesystemContext configuration,
        IRepository<EnduranceEvent> upcomingEvents
    )
        : base(configuration)
    {
        _upcomingEvents = upcomingEvents;
    }

    public async Task<EnduranceEvent?> Get()
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

    public async Task Commit(EnduranceEvent? enduranceEvent)
    {
        var setup = await Transact();
        setup.ConnectedEventId = enduranceEvent?.Id;
        await Commit(setup);
    }
}
