using Not.Application.Behinds;
using Not.Application.Krud;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Phases;

public class PhaseCrudeRepository : KrudInMemoryRepository<Phase>
{
    readonly ISelectedEventContext _rootContext;

    public PhaseCrudeRepository(ICrudeParent<Phase> parentContext, ISelectedEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Phase> Aggregates =>
        _rootContext.Event?.Competitions.SelectMany(x => x.Phases).ToList() ?? [];
}
