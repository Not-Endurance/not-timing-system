using Not.Application.Krud;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Competitions;

public class CompetitionKrudRepository : KrudInMemoryRepository<Competition>
{
    readonly ISelectedEventContext _rootContext;

    public CompetitionKrudRepository(IKrudParentNodeOf<Competition> parentContext, ISelectedEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Competition> Aggregates => _rootContext.Event?.Competitions ?? [];
}
