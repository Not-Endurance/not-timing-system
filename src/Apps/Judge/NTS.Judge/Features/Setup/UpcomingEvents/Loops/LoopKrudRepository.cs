using Not.Application.Behinds;
using Not.Application.Krud;
using NTS.Domain.Setup.Aggregates;
using NTS.Application.Warp;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Loops;

public class LoopKrudRepository : KrudInMemoryRepository<Loop>
{
    readonly ISelectedEventContext _rootContext;

    public LoopKrudRepository(ICrudeParent<Loop> parentContext, ISelectedEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Loop> Aggregates => _rootContext.Event?.Loops ?? [];
}
