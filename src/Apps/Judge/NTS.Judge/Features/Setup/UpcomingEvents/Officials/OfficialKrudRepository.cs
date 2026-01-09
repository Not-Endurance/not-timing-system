using Not.Application.Behinds;
using Not.Application.Krud;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Officials;

public class OfficialKrudRepository : KrudInMemoryRepository<Official>
{
    readonly ISelectedEventContext _rootContext;

    public OfficialKrudRepository(ICrudeParent<Official> parentContext, ISelectedEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Official> Aggregates => _rootContext.Event?.Officials ?? [];
}
