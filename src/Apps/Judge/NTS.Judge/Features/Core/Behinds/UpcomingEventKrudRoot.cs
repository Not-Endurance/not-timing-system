using Not.Application.CRUD.Ports;
using Not.Application.Krud;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Core.Behinds;

public class UpcomingEventKrudRoot
    : KrudNode<UpcomingEvent>,
        IKrudParentNodeOf<Competition>,
        IKrudParentNodeOf<Official>,
        IKrudParentNodeOf<Loop>,
        IKrudParentNodeOf<Combination>
{
    readonly EventRpcContext _rootNode;

    public UpcomingEventKrudRoot(IRepository<UpcomingEvent> parent, EventRpcContext rootNode)
        : base(parent)
    {
        _rootNode = rootNode;
    }

    IReadOnlyList<Competition> IKrudParentNodeOf<Competition>.Children => _rootNode.Event?.Competitions ?? [];
    IReadOnlyList<Official> IKrudParentNodeOf<Official>.Children => _rootNode.Event?.Officials ?? [];
    IReadOnlyList<Loop> IKrudParentNodeOf<Loop>.Children => _rootNode.Event?.Loops ?? [];
    IReadOnlyList<Combination> IKrudParentNodeOf<Combination>.Children => _rootNode.Event?.Combinations ?? [];

    public override async Task Set(object aggregate)
    {
        if (aggregate is not UpcomingEvent upcomingEvent)
        {
            return;
        }
        await _rootNode.SetEvent(upcomingEvent);
    }

    // TODO: replace manual propagation using `IUpdate<T>` with Observable node approach:
    // Each entity in the Krud should have a node (Potentially use refleciton to create nodes from IParent<>) 
    // All nodes implement IKrudNodeSetter, used by Blazor components to set the current instance of their node
    // Parent nodes also implement IKrudParentNodeOf used by KrudService to render the list the children aggregates
    // Parent nodes also Observe children nodes, which ensures that any events in the aggregate are propagated to the Root node
    // Root node updates the state of the aggregate on event
    // Changes in the state of any aggregate Emit a node Event, kickstarting the propagation process
    public async Task Create(Competition item)
    {
        await Add(_rootNode.Event, item);
    }

    public async Task Update(Competition items)
    {
        await Update(_rootNode.Event, items);
    }

    public async Task Delete(IEnumerable<Competition> children)
    {
        await Remove(_rootNode.Event, children);
    }

    public async Task Create(Official item)
    {
        await Add(_rootNode.Event, item);
    }

    public async Task Update(Official items)
    {
        await Update(_rootNode.Event, items);
    }

    public async Task Delete(IEnumerable<Official> children)
    {
        await Remove(_rootNode.Event, children);
    }

    public async Task Create(Loop item)
    {
        await Add(_rootNode.Event, item);
    }

    public async Task Update(Loop items)
    {
        await Update(_rootNode.Event, items);
    }

    public async Task Delete(IEnumerable<Loop> children)
    {
        await Remove(_rootNode.Event, children);
    }

    public async Task Create(Combination item)
    {
        await Add(_rootNode.Event, item);
    }

    public async Task Update(Combination items)
    {
        await Update(_rootNode.Event, items);
    }

    public async Task Delete(IEnumerable<Combination> children)
    {
        await Remove(_rootNode.Event, children);
    }
}
