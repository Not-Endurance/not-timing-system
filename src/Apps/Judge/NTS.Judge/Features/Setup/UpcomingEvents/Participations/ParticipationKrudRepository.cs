using Not.Application.Behinds;
using Not.Application.Krud;
using NTS.Domain.Setup.Aggregates;
using NTS.Application.Warp;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Participations;

public class ParticipationKrudRepository : KrudInMemoryRepository<Participation>
{
    readonly ISelectedEventContext _rootContext;

    public ParticipationKrudRepository(ICrudeParent<Participation> parentContext, ISelectedEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Participation> Aggregates =>
        _rootContext.Event?.Competitions.SelectMany(x => x.Participations).ToList() ?? [];
}
