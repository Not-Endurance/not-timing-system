using Not.Application.Behinds;
using Not.Storage.Repositories;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Officials;

public class OfficialContextRepository : CrudeRepository<Official>
{
    readonly IEventContext _rootContext;

    public OfficialContextRepository(ICrudeParent<Official> parentContext, IEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Official> Aggregates => _rootContext.Event?.Officials ?? [];
}
