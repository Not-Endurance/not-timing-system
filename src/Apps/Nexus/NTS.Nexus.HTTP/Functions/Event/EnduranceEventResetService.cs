using Not.Injection;
using NTS.Nexus.HTTP.Mongo.Repositories;

namespace NTS.Nexus.HTTP.Functions.Event;

public interface IEnduranceEventResetService
{
    Task Reset(int eventId);
}

public class EnduranceEventResetService : IEnduranceEventResetService, ITransient
{
    readonly IEnumerable<IEventResetRepository> _repositories;

    public EnduranceEventResetService(IEnumerable<IEventResetRepository> repositories)
    {
        _repositories = repositories;
    }

    public async Task Reset(int eventId)
    {
        foreach (var repository in _repositories)
        {
            await repository.ResetEvent(eventId);
        }
    }
}
