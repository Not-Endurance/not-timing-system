using System.Collections;
using System.Runtime.CompilerServices;
using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Domain;
using Not.Domain.Krud;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Graph;
using Not.Krud.Models;
using Not.Krud.ServiceRegistration;
using Not.Observables;

namespace Not.Krud.Services;

public class KrudGraphContext<T> : Observer, IKrudNodeSetter, IKrudGraphProvider, IKrudDependencyResolver
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

    public bool Supports(Type principalType)
    {
        EnsureGraphBuilt();
        return FindDependencies(principalType).Any();
    }

    public async Task Reflect<TPrincipal>(TPrincipal principal)
        where TPrincipal : Entity
    {
        EnsureGraphBuilt();
        if (_graph?.Root?.Value is not T root)
        {
            return;
        }

        var pending = new Queue<Entity>();
        var seen = new HashSet<(Type Type, int Id)>();
        pending.Enqueue(principal);
        seen.Add((principal.GetType(), principal.Id));

        var hasReflected = false;
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            var updatedDependents = new HashSet<(Type Type, int Id)>();

            foreach (var usage in ResolveUsages(root, current))
            {
                var dependentKey = (usage.Dependent.GetType(), usage.Dependent.Id);
                if (!updatedDependents.Add(dependentKey))
                {
                    continue;
                }

                if (!KrudReflectionHelper.TryReflect(usage.Dependent, current))
                {
                    continue;
                }

                hasReflected = true;
                if (seen.Add(dependentKey))
                {
                    pending.Enqueue(usage.Dependent);
                }
            }
        }

        if (hasReflected)
        {
            await _coalescedCommit.Invoke();
        }
    }

    public KrudDeleteImpact PreviewDelete(Entity principal)
    {
        EnsureGraphBuilt();
        if (_graph?.Root?.Value is not T root)
        {
            return new KrudDeleteImpact(Label(principal), []);
        }

        var usages = ResolveUsages(root, principal);
        var projection = usages
            .Select(x => new KrudDeleteUsage(x.Dependency.Relation, Label(x.Parent), Label(x.Dependent)))
            .ToList();

        return new KrudDeleteImpact(Label(principal), projection);
    }

    public void CascadeDeleteDependencies(Entity principal)
    {
        EnsureGraphBuilt();
        if (_graph?.Root?.Value is not T root)
        {
            return;
        }

        var usages = ResolveUsages(root, principal);
        var deleted = new HashSet<string>();
        foreach (var usage in usages)
        {
            var key =
                $"{RuntimeHelpers.GetHashCode(usage.Parent)}:{usage.Dependency.DependentType.FullName}:{usage.Dependent.Id}";
            if (!deleted.Add(key))
            {
                continue;
            }
            RemoveDependent(usage.Parent, usage.Dependency.DependentType, usage.Dependent);
        }
    }

    List<KrudDependency> FindDependencies(Type principalType)
    {
        GuardHelper.ThrowIfDefault(_graph);
        return _graph
            .DependenciesByPrincipalType.Where(x => x.Key.IsAssignableFrom(principalType))
            .SelectMany(x => x.Value)
            .ToList();
    }

    List<DependencyUsage> ResolveUsages(T root, Entity principal)
    {
        var dependencies = FindDependencies(principal.GetType());
        if (!dependencies.Any())
        {
            return [];
        }

        var aggregateNodes = Traverse(root).ToList();
        var result = new List<DependencyUsage>();

        foreach (var dependency in dependencies)
        {
            var parents = aggregateNodes.Where(node => dependency.ParentType.IsAssignableFrom(node.GetType()));
            foreach (var parent in parents)
            {
                var children = ReadChildren(parent, dependency.DependentType);
                foreach (var child in children)
                {
                    if (child is not Entity dependent)
                    {
                        continue;
                    }

                    var principalValue = dependency.Property.GetValue(child);
                    if (principalValue is not Entity principalEntity)
                    {
                        continue;
                    }

                    if (principalEntity != principal)
                    {
                        continue;
                    }

                    result.Add(new DependencyUsage(parent, dependent, dependency));
                }
            }
        }

        return result;
    }

    static IEnumerable<object> Traverse(object root)
    {
        var visited = new HashSet<object>(new ReferenceEqualityComparer<object>());
        var queue = new Queue<object>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            yield return current;

            var parentInterfaces = current
                .GetType()
                .GetInterfaces()
                .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IKrudParent<>));

            foreach (var parentInterface in parentInterfaces)
            {
                var childrenProperty = parentInterface.GetProperty(nameof(IKrudParent<Entity>.Children));
                if (childrenProperty?.GetValue(current) is not IEnumerable children)
                {
                    continue;
                }

                foreach (var child in children.OfType<object>())
                {
                    queue.Enqueue(child);
                }
            }
        }
    }

    static IEnumerable<object> ReadChildren(object parent, Type dependentType)
    {
        var parentInterface = typeof(IKrudParent<>).MakeGenericType(dependentType);
        if (!parentInterface.IsAssignableFrom(parent.GetType()))
        {
            return [];
        }

        var childrenProperty = parentInterface.GetProperty(nameof(IKrudParent<Entity>.Children));
        var children = childrenProperty?.GetValue(parent) as IEnumerable;
        return children?.OfType<object>() ?? [];
    }

    static void RemoveDependent(object parent, Type dependentType, Entity dependent)
    {
        var parentInterface = typeof(IKrudParent<>).MakeGenericType(dependentType);
        var removeMethod = parentInterface.GetMethod(nameof(IKrudParent<Entity>.Remove));
        if (removeMethod == null)
        {
            throw new InvalidOperationException(
                $"'{parent.GetType().Name}' does not implement remove for '{dependentType.Name}'."
            );
        }
        removeMethod.Invoke(parent, [dependent]);
    }

    static string Label(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }
        try
        {
            var text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }
        catch { }
        return value.GetType().Name;
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

    sealed class DependencyUsage
    {
        public DependencyUsage(object parent, Entity dependent, KrudDependency dependency)
        {
            Parent = parent;
            Dependent = dependent;
            Dependency = dependency;
        }

        public object Parent { get; }
        public Entity Dependent { get; }
        public KrudDependency Dependency { get; }
    }
}

internal interface IKrudGraphProvider
{
    object GetNodeByClosedParentInterface(Type closedIface);
    IEnumerable<IKrudNodeSetter> GetNodeSetters();
}
