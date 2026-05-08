using Not.Injection;
using NTS.Nexus.HTTP.Mongo.Repositories;

namespace NTS.Nexus.HTTP.Functions.Event;

public interface IEventInformationResetService
{
    Task Reset(int eventId);
}

public class EventInformationResetService : IEventInformationResetService, ITransient
{
    readonly IEnumerable<IEventResetRepository> _repositories;

    public EventInformationResetService(IEnumerable<IEventResetRepository> repositories)
    {
        _repositories = repositories;
    }

    public async Task Reset(int eventId)
    {
        foreach (var repository in _repositories)
        {
            await repository.DeleteAllForEvent(eventId);
        }
    }
}
