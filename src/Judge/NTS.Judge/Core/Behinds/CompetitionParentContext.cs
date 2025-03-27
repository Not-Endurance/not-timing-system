using Not.Application.Behinds;
using Not.Application.CRUD.Ports;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Core.Behinds;

public class CompetitionParentContext : BehindContext<Competition>, ICrudParent<Phase>, ICrudParent<Participation>
{
    public CompetitionParentContext(IRepository<Competition> competitionRepository)
        : base(competitionRepository) { }

    IReadOnlyList<Phase> ICrudParent<Phase>.Children => Entity!.Phases;
    IReadOnlyList<Participation> ICrudParent<Participation>.Children => Entity!.Participations;

    public async Task Add(Phase child)
    {
        Entity!.Add(child);
        await Persist();
    }

    public async Task Update(Phase child)
    {
        Entity!.Update(child);
        await Persist();
    }

    public async Task Remove(Phase child)
    {
        Entity!.Remove(child);
        await Persist();
    }

    public async Task Add(Participation child)
    {
        Entity!.Add(child);
        await Persist();
    }

    public async Task Update(Participation child)
    {
        Entity!.Update(child);
        await Persist();
    }

    public async Task Remove(Participation child)
    {
        Entity!.Remove(child);
        await Persist();
    }
}
