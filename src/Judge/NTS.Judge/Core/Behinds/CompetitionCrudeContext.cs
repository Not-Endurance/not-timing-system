using Not.Application.Behinds;
using Not.Domain;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Core.Behinds;

public class CompetitionCrudeContext : CrudeContext<Competition>, ICrudeParent<Phase>, ICrudeParent<Participation>
{
    Competition? _competition;

    public CompetitionCrudeContext(ICrudeParent<Competition> parentContext) : base(parentContext)
    {
    }

    IReadOnlyList<Phase> ICrudeParent<Phase>.Children => _competition?.Phases ?? [];
    IReadOnlyList<Participation> ICrudeParent<Participation>.Children => _competition?.Participations ?? [];

    public override void Set(IParent parent)
    {
        if (parent is not Competition competition)
        {
            return;
        }
        _competition = competition;
    }

    public async Task Add(Phase child)
    {
        await Add(_competition, child);
    }

    public async Task Propagate(Phase child)
    {
        await Update(_competition, child);
    }

    public async Task Remove(params IEnumerable<Phase> children)
    {
        await Remove(_competition, children);
    }

    public async Task Add(Participation child)
    {
        await Add(_competition, child);
    }

    public async Task Propagate(Participation child)
    {
        await Update(_competition, child);
    }

    public async Task Remove(IEnumerable<Participation> children)
    {
        await Remove(_competition, children);
    }
}
