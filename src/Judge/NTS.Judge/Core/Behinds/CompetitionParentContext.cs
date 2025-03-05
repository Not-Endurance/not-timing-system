using Not.Application.Behinds;
using Not.Application.CRUD.Ports;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Core.Behinds;

public class CompetitionParentContext
    : BehindContext<Competition>,
        IParentContext<Phase>,
        IParentContext<Participation>
{
    readonly ObservableList<Phase> _phases = new();
    readonly ObservableList<Participation> _participations = new();

    public CompetitionParentContext(IRepository<Competition> competitionRepository)
        : base(competitionRepository) { }

    ObservableList<Phase> IParentContext<Phase>.Children => _phases;
    ObservableList<Participation> IParentContext<Participation>.Children => _participations;

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
