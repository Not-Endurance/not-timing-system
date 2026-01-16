using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Graph;
using Not.Application.Krud.ServiceRegistration;
using Not.Domain.Aggregates;
using Not.Exceptions;
using Not.Observables;

namespace Not.Application.Krud.Services;

public class KrudGraphContext<T> : Observer, IKrudNodeSetter, IKrudGraphProvider
    where T : AggregateRoot
{
    readonly IRepository<T> _repository;
    readonly SemaphoreSlim _commitGate = new(1, 1);
    readonly Type _rootType;
    int _isCommitPending;
    KrudGraph? _graph;
    Dictionary<Type, object> _nodesByClosedParentInterface = [];

    public KrudGraphContext(IRepository<T> repository)
    {
        _repository = repository;
        _rootType = typeof(T);
    }

    public void SetParent(object aggregate)
    {
        GuardHelper.ThrowIfDefault(_graph?.Root);
        if (aggregate.GetType() != _rootType)
        {
            return;
        }
        _graph.Root.Set(aggregate);
    }

    public object GetNodeByClosedParentInterface(Type closedIface)
    {
        EnsureGraphBuilt();
        if (!_nodesByClosedParentInterface.TryGetValue(closedIface, out var node))
        {
            throw new InvalidOperationException($"Krud node not found for interface '{closedIface.FullName}'");
        }
        return node;
    }

    public IEnumerable<IKrudNodeSetter> GetNodeSetters()
    {
        EnsureGraphBuilt();
        return [this, .. _graph!.AllNodes.OfType<IKrudNodeSetter>()];
    }

    void EnsureGraphBuilt()
    {
        if (_graph != null)
        {
            return;
        }

        _graph = KrudGraphHelper.Build(typeof(T));
        if (_graph.IsFlatAggregate)
        {
            throw new InvalidOperationException(
                $"Flat aggregate '{typeof(T).FullName}' does not need '{nameof(KrudGraphContext<T>)}' instance"
            );
        }

        _nodesByClosedParentInterface = _graph
            .AllNodes.SelectMany(x =>
                KrudReflectionHelper
                    .GetClosedKrudParentInterfaces(x.GetType())
                    .Select(y => (@interface: y, node: (object)x))
            )
            .GroupBy(x => x.@interface)
            .ToDictionary(x => x.Key, x => x.First().node);

        Observe(_graph.Root, CommitCoalesced);
    }

    async Task CommitCoalesced()
    {
        GuardHelper.ThrowIfDefault(_graph!.Root!.Value);
        Interlocked.Exchange(ref _isCommitPending, 1);
        await StartCommitLoop((T)_graph.Root.Value);
    }

    async Task StartCommitLoop(T aggregate)
    {
        if (!await _commitGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            while (Interlocked.Exchange(ref _isCommitPending, 0) == 1)
            {
                await _repository.Update(aggregate);
            }
        }
        finally
        {
            _commitGate.Release();
        }
    }
}

internal interface IKrudGraphProvider
{
    object GetNodeByClosedParentInterface(Type closedIface);
    IEnumerable<IKrudNodeSetter> GetNodeSetters();
}
