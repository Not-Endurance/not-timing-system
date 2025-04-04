using Not.Application.Behinds;
using Not.Application.CRUD.Ports;
using Not.Domain;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Core.Behinds;

public class CompetitionParentContext : BehindContext<Competition>, ICrudParent<Phase>, ICrudParent<Participation>
{
    ObservableList<Phase> _phases = new();
    ObservableList<Participation> _participations = new();

    public CompetitionParentContext(IRepository<Competition> competitionRepository)
        : base(competitionRepository) { }

    ObservableList<Phase> ICrudParent<Phase>.Children => _phases;
    ObservableList<Participation> ICrudParent<Participation>.Children => _participations;

    public override void SetParent(IParent parent)
    {
        if (parent is not Competition competition)
        {
            return;
        }
        Entity = competition;
        _phases = new(competition.Phases);
        _participations = new(competition.Participations);
    }

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
