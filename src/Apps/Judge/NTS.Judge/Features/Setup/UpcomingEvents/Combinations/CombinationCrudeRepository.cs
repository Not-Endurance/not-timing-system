using Not.Application.Behinds;
using Not.Storage.Repositories;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Combinations;

public class CombinationCrudeRepository : CrudeRepository<Combination>
{
    readonly IEventContext _rootContext;

    public CombinationCrudeRepository(ICrudeParent<Combination> parentContext, IEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Combination> Aggregates => _rootContext.Event?.Combinations ?? [];
}
