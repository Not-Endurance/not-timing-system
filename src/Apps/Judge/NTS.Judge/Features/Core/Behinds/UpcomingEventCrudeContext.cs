using Not.Application.Behinds;
using Not.Application.CRUD.Ports;
using Not.Domain;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Core.Behinds;

public class UpcomingEventCrudeContext
    : CrudeContext<UpcomingEvent>,
        ICrudeParent<Competition>,
        ICrudeParent<Official>,
        ICrudeParent<Loop>,
        ICrudeParent<Combination>
{
    readonly EventRpcContext _eventContext;

    public UpcomingEventCrudeContext(IRepository<UpcomingEvent> parent, EventRpcContext eventContext)
        : base(parent)
    {
        _eventContext = eventContext;
    }

    IReadOnlyList<Competition> ICrudeParent<Competition>.Children => _eventContext.Event?.Competitions ?? [];
    IReadOnlyList<Official> ICrudeParent<Official>.Children => _eventContext.Event?.Officials ?? [];
    IReadOnlyList<Loop> ICrudeParent<Loop>.Children => _eventContext.Event?.Loops ?? [];
    IReadOnlyList<Combination> ICrudeParent<Combination>.Children => _eventContext.Event?.Combinations ?? [];

    public override async Task Set(IParent parent)
    {
        if (parent is not UpcomingEvent upcomingEvent)
        {
            return;
        }
        await _eventContext.SetEvent(upcomingEvent);
    }

    public async Task Create(Competition item)
    {
        await Add(_eventContext.Event, item);
    }

    public async Task Update(Competition items)
    {
        await Update(_eventContext.Event, items);
    }

    public async Task Delete(IEnumerable<Competition> children)
    {
        await Remove(_eventContext.Event, children);
    }

    public async Task Create(Official item)
    {
        await Add(_eventContext.Event, item);
    }

    public async Task Update(Official items)
    {
        await Update(_eventContext.Event, items);
    }

    public async Task Delete(IEnumerable<Official> children)
    {
        await Remove(_eventContext.Event, children);
    }

    public async Task Create(Loop item)
    {
        await Add(_eventContext.Event, item);
    }

    public async Task Update(Loop items)
    {
        await Update(_eventContext.Event, items);
    }

    public async Task Delete(IEnumerable<Loop> children)
    {
        await Remove(_eventContext.Event, children);
    }

    public async Task Create(Combination item)
    {
        await Add(_eventContext.Event, item);
    }

    public async Task Update(Combination items)
    {
        await Update(_eventContext.Event, items);
    }

    public async Task Delete(IEnumerable<Combination> children)
    {
        await Remove(_eventContext.Event, children);
    }
}
