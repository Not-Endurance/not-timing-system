using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Competitions;

public class CompetitionKrudNode : ManualKrudNode<Competition>, IKrudParentNodeOf<Phase>, IKrudParentNodeOf<Participation>
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

    public async Task Delete(Phase item)
    {
        await Remove(_competition, item);
    }

    public async Task Create(Participation item)
    {
        await Add(_competition, item);
    }

    public async Task Update(Participation items)
    {
        await Update(_competition, items);
    }

    public async Task Delete(Participation item)
    {
        await Remove<Competition, Participation>(_competition, item);
    }
}
