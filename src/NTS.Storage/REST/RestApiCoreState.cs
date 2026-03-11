using Not.Application.CRUD.Ports;
using Not.Domain.Abstractions;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Features.Core;

namespace NTS.Storage.REST;

public class RestApiCoreState : ICoreState, ISingleton
{
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Official> _officials;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<SnapshotResult> _snapshotResults;
    readonly IRepository<Handout> _handouts;

    public RestApiCoreState(
        IRepository<EnduranceEvent> events,
        IRepository<Official> officials,
        IRepository<Participation> participations,
        IRepository<Ranking> rankings,
        IRepository<SnapshotResult> snapshotResults,
        IRepository<Handout> handouts
    )
    {
        _events = events;
        _officials = officials;
        _participations = participations;
        _rankings = rankings;
        _snapshotResults = snapshotResults;
        _handouts = handouts;
    }

    public async Task Reset()
    {
        await DeleteAll(_handouts);
        await DeleteAll(_snapshotResults);
        await DeleteAll(_rankings);
        await DeleteAll(_participations);
        await DeleteAll(_officials);

        var current = await _events.Read(0);
        if (current != null)
        {
            await _events.Delete(current);
        }
    }

    static async Task DeleteAll<T>(IRepository<T> repository)
        where T : class, IEntity
    {
        var items = await repository.ReadMany();
        await repository.Delete(items);
    }
}
