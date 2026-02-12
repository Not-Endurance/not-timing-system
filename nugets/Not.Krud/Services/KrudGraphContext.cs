using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Domain;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Graph;
using Not.Krud.ServiceRegistration;
using Not.Observables;

namespace Not.Krud.Services;

public class KrudGraphContext<T> : Observer, IKrudNodeSetter, IKrudGraphProvider
    where T : Aggregate
{
    readonly CoalesceInvoker _coalescedCommit;
    readonly IRepository<T> _repository;
    readonly Type _rootType;
    KrudGraph? _graph;
    Dictionary<Type, object> _nodesByClosedParentInterface = [];

    public KrudGraphContext(IRepository<T> repository)
    {
        _repository = repository;
        _rootType = typeof(T);
        _coalescedCommit = new(() => _repository.Update(Aggregate));
    }

    protected T Aggregate => (T)_graph!.Root!.Value!;

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

        Observe(_graph.Root, _coalescedCommit.Invoke);
    }
}

internal interface IKrudGraphProvider
{
    object GetNodeByClosedParentInterface(Type closedIface);
    IEnumerable<IKrudNodeSetter> GetNodeSetters();
}
