using Not.Application.Behinds;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Core.Behinds;

public class EventParentContext : BehindContext<EnduranceEvent>, ICrudParent<Competition>, ICrudParent<Official>
{
    public EventParentContext(IUpdate<EnduranceEvent> updater)
        : base(updater) { }

    IReadOnlyList<Competition> ICrudParent<Competition>.Children => Entity!.Competitions;
    IReadOnlyList<Official> ICrudParent<Official>.Children => Entity!.Officials;

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
