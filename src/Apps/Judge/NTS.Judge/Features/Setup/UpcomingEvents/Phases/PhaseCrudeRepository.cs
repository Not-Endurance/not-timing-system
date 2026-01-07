using Not.Application.Behinds;
using Not.Storage.Repositories;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Phases;

public class PhaseCrudeRepository : CrudeRepository<Phase>
{
    readonly IEventContext _rootContext;

    public PhaseCrudeRepository(ICrudeParent<Phase> parentContext, IEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Phase> Aggregates =>
        _rootContext.Event?.Competitions.SelectMany(x => x.Phases).ToList() ?? [];
}
