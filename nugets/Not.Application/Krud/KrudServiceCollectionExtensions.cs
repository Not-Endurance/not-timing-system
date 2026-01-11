using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Nodes;
using Not.Application.Krud.Services;
using Not.Domain;
using Not.Domain.Aggregates;
using Not.Startup;

namespace Not.Application.Krud;

public static class KrudServiceCollectionExtensions
{
    /// <summary>
    /// Registers:
    /// - KrudTree&lt;TRoot&gt;
    /// - All node instances produced from scanning TRoot's reachable aggregate graph
    /// - Each node as its closed IKrudV1ParentNodeOf&lt;TChild&gt; interfaces (critical for your repositories)
    /// - Each node as IKrudNodeSetter (optional, but handy)
    /// - Each node as KrudNode (optional, for diagnostics / tooling)
    /// </summary>
    public static IServiceCollection AddKrudV1Tree<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where T : AggregateRoot
    {
        var rootType = typeof(T);
        var built = KrudV1GraphBuilder.Build(rootType);

        // Simple Aggregate without any children, then we don't need the the grap since we don't have levels to bubble updates over
        if (!built.AllNodes.Any())
        {
            return services;
        }

        services.Add(new ServiceDescriptor(typeof(IStartupInitializer), typeof(KrudGraphContext<T>), lifetime));

        // Register nodes as instances + interfaces
        foreach (var node in built.AllNodes)
        {
            if (node.IsRoot)
            {
                var root = new KrudRootProvider<T>();
                root.SetRootNode(node);
                services.Add(new ServiceDescriptor(typeof(KrudRootProvider<T>), _ => root, lifetime));
                services.Add(new ServiceDescriptor(typeof(IKrudNodeSetter), _ => root, lifetime));
            }

            // Concrete
            services.Add(new ServiceDescriptor(node.GetType(), _ => node, lifetime));


            // Optional: setter abstraction
            if (node is IKrudNodeSetter setter)
            {
                services.Add(new ServiceDescriptor(typeof(IKrudNodeSetter), _ => setter, lifetime));
            }

            // Critical: register as all closed IKrudV1ParentNodeOf<T> it implements
            foreach (var iface in KrudReflection.GetClosedKrudParentInterfaces(node.GetType()))
            {
                // iface is IKrudV1ParentNodeOf<SomeChildAggregate>
                services.Add(new ServiceDescriptor(iface, _ => node, lifetime));
            }
        }

        // If you want to expose graph metadata later:
        // services.Add(new ServiceDescriptor(typeof(KrudV1GraphMetadata), _ => built.Metadata, lifetime));

        return services;
    }
}

internal static class KrudV1GraphBuilder
{
    internal sealed record BuildResult(
        IReadOnlyList<KrudNode> AllNodes,
        IReadOnlyDictionary<Type, KrudNode> GroupNodeByParentType);

    public static BuildResult Build(Type rootType)
    {
        // 1) discover reachable aggregate types from the root type
        var reachable = KrudReflection.DiscoverReachableAggregateTypes(rootType);

        // 2) compute parent->children (from IParent<TChild>)
        var childrenByParent = reachable.ToDictionary(
            t => t,
            t => KrudReflection.GetChildAggregateTypesFromIParent(t)
                .Where(reachable.Contains)
                .Distinct()
                .ToList()
        );

        // 3) post-order traversal so children are processed before parents
        var ordered = KrudReflection.TopologicalPostOrder(rootType, childrenByParent);

        // 4) build group nodes bottom-up
        var groupByType = new Dictionary<Type, KrudNode>();
        var allNodes = new HashSet<KrudNode>(ReferenceEqualityComparer<KrudNode>.Instance);

        foreach (var parentType in ordered)
        {
            var childTypes = childrenByParent[parentType];
            if (childTypes.Count == 0)
                continue; // leaf

            // Build edge nodes for each (parentType -> childType):
            // Edge node is KrudV1ParentNodeOf<childType>, and it observes the child "group node" (if any),
            // so that edits inside child bubble up to this edge.
            var edgeNodes = new List<KrudNode>(childTypes.Count);

            foreach (var childType in childTypes)
            {
                var edgeNode = KrudReflection.CreateParentEdgeNode(childType);

                if (groupByType.TryGetValue(childType, out var childGroupNode))
                {
                    // childGroupNode is what emits when the child aggregate's internal children change
                    edgeNode.AttachChildren(new[] { childGroupNode });
                }

                edgeNodes.Add(edgeNode);
                allNodes.Add(edgeNode);
            }

            // Group node for the parentType:
            // - if only one edge, the edge node itself *is* the group node
            // - else composite node observes edge nodes (for propagation) while exposing flattened "semantic children"
            KrudNode groupNode;
            if (edgeNodes.Count == 1)
            {
                groupNode = edgeNodes[0];
            }
            else
            {
                groupNode = new KrudNode();

                // Important:
                // - composite must OBSERVE the edge nodes to catch direct Add/Update/Remove on them
                // - composite may EXPOSE children as flattened union of edge.Children (your semantic requirement)
                groupNode.AttachChildren(edgeNodes);
                allNodes.Add(groupNode);
            }

            if (parentType == rootType)
            {
                groupNode.IsRoot = true;
            }

            groupByType[parentType] = groupNode;
        }

        return new BuildResult(allNodes.ToList(), groupByType);
    }
}

internal static class KrudReflection
{
    public static IEnumerable<Type> GetChildAggregateTypesFromIParent(Type parentType)
    {
        // Parent implements IParent<TChild> for each child type
        return parentType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IParent<>))
            .Select(i => i.GetGenericArguments()[0])
            .Where(t => typeof(Aggregate).IsAssignableFrom(t));
    }

    public static HashSet<Type> DiscoverReachableAggregateTypes(Type rootType)
    {
        // Follows public instance properties to find Aggregate / AggregateRoot types.
        // If you keep children in private fields, you’ll want to extend this.
        var result = new HashSet<Type>();
        var queue = new Queue<Type>();
        queue.Enqueue(rootType);

        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            if (!result.Add(t))
                continue;

            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var candidate in UnwrapPropertyTypes(p.PropertyType))
                {
                    if (typeof(Aggregate).IsAssignableFrom(candidate) ||
                        typeof(AggregateRoot).IsAssignableFrom(candidate))
                    {
                        queue.Enqueue(candidate);
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<Type> UnwrapPropertyTypes(Type type)
    {
        // IEnumerable<T> (except string) -> T
        if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
        {
            if (type.IsGenericType)
            {
                yield return type.GetGenericArguments()[0];
                yield break;
            }
        }

        yield return type;
    }

    public static List<Type> TopologicalPostOrder(
        Type root,
        IDictionary<Type, List<Type>> childrenByParent)
    {
        var visited = new HashSet<Type>();
        var visiting = new HashSet<Type>(); // cycle detection
        var output = new List<Type>();

        void Dfs(Type t)
        {
            if (visited.Contains(t))
                return;

            if (!visiting.Add(t))
                throw new InvalidOperationException($"Cycle detected in KRUD type graph at {t.FullName}");

            if (childrenByParent.TryGetValue(t, out var children))
            {
                foreach (var c in children)
                    Dfs(c);
            }

            visiting.Remove(t);
            visited.Add(t);
            output.Add(t); // post-order
        }

        Dfs(root);
        return output;
    }

    public static KrudNode CreateParentEdgeNode(Type childType)
    {
        // Edge node type is KrudV1ParentNodeOf<TChild>
        var nodeType = typeof(KrudParentNodeOf<>).MakeGenericType(childType);

        // Must be parameterless ctor (per your latest change)
        return (KrudNode)Activator.CreateInstance(nodeType)!;
    }

    public static IEnumerable<Type> GetClosedKrudParentInterfaces(Type nodeType)
    {
        // Finds IKrudV1ParentNodeOf<T> (closed) on the node type
        return nodeType.GetInterfaces()
            .Where(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IKrudParentNodeOf<>));
    }
}

/// <summary>
/// Ensures HashSet uses reference equality for nodes (so we don’t accidentally drop distinct instances).
/// </summary>
internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    public static ReferenceEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
