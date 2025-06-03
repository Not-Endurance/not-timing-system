using Not.Application.Behinds;
using Not.Storage.Repositories;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Setup.Participations;

public class ParticipationCrudeRepository : CrudeRepository<Participation>
{
    readonly IEventContext _rootContext;

    public ParticipationCrudeRepository(ICrudeParent<Participation> parentContext, IEventContext rootContext)
        : base(parentContext)
    {
        _rootContext = rootContext;
    }

    protected override IReadOnlyList<Participation> Aggregates =>
        _rootContext.Event?.Competitions.SelectMany(x => x.Participations).ToList() ?? [];
}
