using Not.Application.Behinds;
using Not.Storage.Repositories;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Loops;

public class LoopCrudeRepository : CrudeRepository<Loop>
{
    readonly IEventContext _rootContext;

    public LoopCrudeRepository(ICrudeParent<Loop> parentContext, IEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Loop> Aggregates => _rootContext.Event?.Loops ?? [];
}
