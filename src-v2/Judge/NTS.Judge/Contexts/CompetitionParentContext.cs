using Not.Application.Contexts;
using Not.Application.Ports.CRUD;
using Not.Structures;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Contexts;

public class CompetitionParentContext(IRepository<Competition> entities) : BehindContext<Competition>(entities),
    IParentContext<Phase>,
    IParentContext<Contestant>
{
    readonly ObservableList<Phase> _phases = new();
    readonly ObservableList<Contestant> _contestants = new();

    ObservableList<Phase> IParentContext<Phase>.Children => _phases;
    ObservableList<Contestant> IParentContext<Contestant>.Children => _contestants;

    public async Task Load(int parentId)
    {
        Entity = await Repository.Read(parentId);
        if (Entity == null)
        {
            return;
        }
        _phases.AddRange(Entity.Phases);
        _contestants.AddRange(Entity.Contestants);
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

    public void Add(Contestant child)
    {
        Entity!.Add(child);
    }
    public void Update(Contestant child)
    {
        Entity!.Update(child);
    }
    public void Remove(Contestant child)
    {
        Entity!.Remove(child);
    }
}
