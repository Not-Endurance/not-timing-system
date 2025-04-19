using Not.Application.Behinds;
using Not.Storage.Repositories;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Competitions;

public class CompetitionCrudeRepository : CrudeRepository<Competition>
{
    readonly IEventContext _rootContext;

    public CompetitionCrudeRepository(ICrudeParent<Competition> parentContext, IEventContext rootContext) : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Competition> Aggregates =>  _rootContext.Event?.Competitions ?? [];
}
