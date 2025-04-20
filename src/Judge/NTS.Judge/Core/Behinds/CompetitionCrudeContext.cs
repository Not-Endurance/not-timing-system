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

    public async Task Create(Phase item)
    {
        await Add(_competition, item);
    }

    public async Task Update(Phase items)
    {
        await Update(_competition, items);
    }

    public async Task Delete(params IEnumerable<Phase> children)
    {
        await Remove(_competition, children);
    }

    public async Task Create(Participation item)
    {
        await Add(_competition, item);
    }

    public async Task Update(Participation items)
    {
        await Update(_competition, items);
    }

    public async Task Delete(IEnumerable<Participation> children)
    {
        await Remove(_competition, children);
    }
}
