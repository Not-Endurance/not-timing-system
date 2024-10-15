using Not.Application.Contexts;
using Not.Application.Ports.CRUD;
using Not.Structures;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Contexts;

public class EventParentContext(IRepository<Event> entities) : BehindContext<Event>(entities),
    IParentContext<Competition>,
    IParentContext<Official>
{
    readonly ObservableList<Competition> _competitions = new();
    readonly ObservableList<Official> _officials = new();

    ObservableList<Competition> IParentContext<Competition>.Children => _competitions;
    ObservableList<Official> IParentContext<Official>.Children => _officials;

    public async Task Load(int parentId)
    {
        Entity = await Repository.Read(parentId);
        if (Entity == null)
        {
            return;
        }
        _competitions.AddRange(Entity!.Competitions);
        _officials.AddRange(Entity!.Officials);
    }

    public void Add(Competition child)
    {
        Entity!.Add(child);
    }
    public void Update(Competition child)
    {
        Entity!.Update(child);
    }
    public void Remove(Competition child)
    {
        Entity!.Remove(child);
    }

    public void Add(Official child)
    {
        Entity?.Add(child);
    }
    public void Update(Official child)
    {
        Entity!.Update(child);
    }
    public void Remove(Official child)
    {
        Entity!.Remove(child);
    }
}
