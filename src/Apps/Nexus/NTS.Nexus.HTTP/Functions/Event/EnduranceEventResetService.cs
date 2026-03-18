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
        var currentDeletedVersion = (
            await Task.WhenAll(_repositories.Select(x => x.GetMaxDeletedVersion(eventId)))
        ).Max();
        var deletedVersion = (currentDeletedVersion ?? 0) + 1;

        foreach (var repository in _repositories)
        {
            await repository.SoftDelete(eventId, deletedVersion);
        }
    }
}
