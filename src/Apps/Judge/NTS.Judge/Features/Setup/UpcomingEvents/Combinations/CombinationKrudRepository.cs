using Not.Application.Krud;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Combinations;

public class CombinationKrudRepository : KrudInMemoryRepository<Combination>
{
    readonly ISelectedEventContext _rootContext;

    public CombinationKrudRepository(IKrudParentNodeOf<Combination> parentContext, ISelectedEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Combination> Aggregates => _rootContext.Event?.Combinations ?? [];
}
