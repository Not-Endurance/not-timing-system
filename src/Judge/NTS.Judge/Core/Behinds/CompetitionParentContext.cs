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

    public async Task Load(int parentId)
    {
        Entity = await Repository.Read(parentId);
        if (Entity == null)
        {
            return;
        }
        _phases.AddRange(Entity.Phases);
        _participations.AddRange(Entity.Participations);
    }

    public void Add(Phase child)
    {
        Entity!.Add(child);
    }

    public void Update(Phase child)
    {
        Entity!.Update(child);
    }

    public void Remove(Phase child)
    {
        Entity!.Remove(child);
    }

    public void Add(Participation child)
    {
        Entity!.Add(child);
    }

    public void Update(Participation child)
    {
        Entity!.Update(child);
    }

    public void Remove(Participation child)
    {
        Entity!.Remove(child);
    }
}
