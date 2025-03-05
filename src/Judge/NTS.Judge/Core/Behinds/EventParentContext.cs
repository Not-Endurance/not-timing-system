using Not.Application.Behinds;
using Not.Application.CRUD.Ports;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Core.Behinds;

public class EventParentContext
    : BehindContext<EnduranceEvent>,
        IParentContext<Competition>,
        IParentContext<Official>
{
    readonly ObservableList<Competition> _competitions = new();
    readonly ObservableList<Official> _officials = new();

    public EventParentContext(IRepository<EnduranceEvent> repository)
        : base(repository) { }

    ObservableList<Competition> IParentContext<Competition>.Children => _competitions;
    ObservableList<Official> IParentContext<Official>.Children => _officials;

    public async Task Add(Competition child)
    {
        Entity!.Add(child);
        await Persist();
    }

    public async Task Update(Competition child)
    {
        Entity!.Update(child);
        await Persist();
    }

    public async Task Remove(Competition child)
    {
        Entity!.Remove(child);
        await Persist();
    }

    public async Task Add(Official child)
    {
        Entity?.Add(child);
        await Persist();
    }

    public async Task Update(Official child)
    {
        Entity!.Update(child);
        await Persist();
    }

    public async Task Remove(Official child)
    {
        Entity!.Remove(child);
        await Persist();
    }
}
