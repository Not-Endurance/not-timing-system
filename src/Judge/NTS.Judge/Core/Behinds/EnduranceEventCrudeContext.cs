using Not.Application.Behinds;
using Not.Application.CRUD.Ports;
using Not.Domain;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Core.Behinds;

public class EnduranceEventCrudeContext : BehindContext<EnduranceEvent>, ICrudParent<Competition>, ICrudParent<Official>
{
    ObservableList<Competition> _competitions = new();
    ObservableList<Official> _officials = new();

    public EnduranceEventCrudeContext(IUpdate<EnduranceEvent> updater)
        : base(updater) { }

    ObservableList<Competition> ICrudParent<Competition>.Children => _competitions;
    ObservableList<Official> ICrudParent<Official>.Children => _officials;

    public override void SetParent(IParent parent)
    {
        if (parent is not EnduranceEvent enduranceEvent)
        {
            return;
        }
        Entity = enduranceEvent;
        _competitions = new(enduranceEvent.Competitions);
        _officials = new(enduranceEvent.Officials);
    }

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
