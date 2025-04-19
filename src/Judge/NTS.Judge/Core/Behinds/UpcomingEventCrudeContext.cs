using Not.Application.Behinds;
using Not.Application.CRUD.Ports;
using Not.Domain;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features;

namespace NTS.Judge.Core.Behinds;

public class UpcomingEventCrudeContext : CrudeContext<UpcomingEvent>, ICrudeParent<Competition>, ICrudeParent<Official>, ICrudeParent<Loop>, ICrudeParent<Combination>
{
    readonly EventContext _eventContext;

    public UpcomingEventCrudeContext(ICrudePropagator<UpcomingEvent> crude, EventContext eventContext) : base(crude)
    {
        _eventContext = eventContext;
    }

    IReadOnlyList<Competition> ICrudeParent<Competition>.Children => _eventContext.Event?.Competitions ?? [];
    IReadOnlyList<Official> ICrudeParent<Official>.Children => _eventContext.Event?.Officials ?? [];
    IReadOnlyList<Loop> ICrudeParent<Loop>.Children => _eventContext.Event?.Loops ?? [];
    IReadOnlyList<Combination> ICrudeParent<Combination>.Children => _eventContext.Event?.Combinations ?? [];

    public override void Set(IParent parent)
    {
        if (parent is not UpcomingEvent upcomingEvent)
        {
            return;
        }
        _eventContext.Event = upcomingEvent;
    }

    public async Task Add(Competition child)
    {
        await Add(_eventContext.Event, child);
    }

    public async Task Propagate(Competition child)
    {
        await Update(_eventContext.Event, child);
    }

    public async Task Remove(IEnumerable<Competition> children)
    {
        await Remove(_eventContext.Event, children);
    }

    public async Task Add(Official child)
    {
        await Add(_eventContext.Event, child);
    }

    public async Task Propagate(Official child)
    {
        await Update(_eventContext.Event, child);
    }

    public async Task Remove(IEnumerable<Official> children)
    {
        await Remove(_eventContext.Event, children);
    }
    
    public async Task Add(Loop child)
    {
        await Add(_eventContext.Event, child);
    }

    public async Task Propagate(Loop child)
    {
        await Update(_eventContext.Event, child);
    }

    public async Task Remove(IEnumerable<Loop> children)
    {
        await Remove(_eventContext.Event, children);
    }

    public async Task Add(Combination child)
    {
        await Add(_eventContext.Event, child);
    }

    public async Task Propagate(Combination child)
    {
        await Update(_eventContext.Event, child);
    }

    public async Task Remove(IEnumerable<Combination> children)
    {
        await Remove(_eventContext.Event, children);
    }
}

public class RootUpdater : ICrudePropagator<UpcomingEvent>
{
    readonly IUpdate<UpcomingEvent> _updater;

    public RootUpdater(IUpdate<UpcomingEvent> updater)
    {
        _updater = updater;
    }
    
    public async Task Propagate(UpcomingEvent aggregate)
    {
        await _updater.Update(aggregate);
    }
}
