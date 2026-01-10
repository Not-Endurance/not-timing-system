using Not.Application.Krud;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Core.Behinds;

public class CompetitionKrudNode : KrudNode<Competition>, IKrudParentNodeOf<Phase>, IKrudParentNodeOf<Participation>
{
    Competition? _competition;

    public CompetitionKrudNode(IKrudParentNodeOf<Competition> parentNode)
        : base(parentNode) { }

    IReadOnlyList<Phase> IKrudParentNodeOf<Phase>.Children => _competition?.Phases ?? [];
    IReadOnlyList<Participation> IKrudParentNodeOf<Participation>.Children => _competition?.Participations ?? [];

    public override Task Set(object aggregate)
    {
        if (aggregate is Competition competition)
        {
            _competition = competition;
        }
        return Task.CompletedTask;
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
