using Not.Injection;
using NTS.Nexus.HTTP.Mongo.Repositories;

namespace NTS.Nexus.HTTP.Functions.Event;

public interface IEventInformationResetService
{
    Task Reset(Guid eventId);
}

public class EventInformationResetService : IEventInformationResetService, ITransient
{
    readonly IEnumerable<IEventResetRepository> _repositories;

    public EventInformationResetService(IEnumerable<IEventResetRepository> repositories)
    {
        _repositories = repositories;
    }

    public async Task Reset(Guid eventId)
    {
        foreach (var repository in _repositories)
        {
            await repository.DeleteAllForEvent(eventId);
        }
    }
}
