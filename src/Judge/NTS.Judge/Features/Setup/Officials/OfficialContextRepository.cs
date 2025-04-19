using Not.Application.Behinds;
using Not.Storage.Repositories;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Officials;

public class OfficialContextRepository : CrudeRepository<Official>
{
    readonly IEventContext _rootContext;

    public OfficialContextRepository(ICrudeParent<Official> parentContext, IEventContext rootContext) : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Official> Aggregates => _rootContext.Event?.Officials ?? [];
}
