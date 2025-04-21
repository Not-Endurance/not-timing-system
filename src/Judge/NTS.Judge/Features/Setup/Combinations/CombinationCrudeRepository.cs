using Not.Application.Behinds;
using Not.Storage.Repositories;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Combinations;

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
