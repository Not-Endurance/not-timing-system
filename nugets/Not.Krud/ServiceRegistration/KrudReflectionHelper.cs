using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Not.Domain;
using Not.Domain.Krud;
using Not.Krud.Abstractions;
using Not.Krud.Graph;

namespace Not.Krud.ServiceRegistration;

internal static class KrudReflectionHelper
{
    static readonly ConcurrentDictionary<(Type DependentType, Type PrincipalType), MethodInfo?> MIRROR_METHODS = [];

    /// <summary>
    /// Returns all direct child <seealso cref="Entity"/>s that also implement <seealso cref="IParent{T}"/>
    /// </summary>
    /// <param name="entityType">Domain entity type</param>
    /// <returns>All edge types of <paramref name="entityType"/></returns>
    public static IEnumerable<Type> GetEdgeAggregatesOf(Type entityType)
    {
        return entityType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IParent<>))
            .Select(i => i.GetGenericArguments()[0])
            .Where(t => typeof(Entity).IsAssignableFrom(t));
    }

    /// <summary>
    /// Scans an Aggregate for inner Entities
    /// </summary>
    /// <param name="aggregateRootType">The aggregate root type to scan for inner entities</param>
    /// <returns></returns>
    public static HashSet<Type> GetEntityTypesIn(Type aggregateRootType)
    {
        var result = new HashSet<Type>();
        var queue = new Queue<Type>();
        queue.Enqueue(aggregateRootType);

        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            if (!result.Add(t))
            {
                continue;
            }

            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var candidate in UnwrapPropertyTypes(p.PropertyType))
                {
                    if (typeof(Entity).IsAssignableFrom(candidate) || typeof(Aggregate).IsAssignableFrom(candidate))
                    {
                        queue.Enqueue(candidate);
                    }
                }
            }
        }

        return result;
    }

    public static List<Type> OrderUsingDepthFirstSearch(Type root, IDictionary<Type, List<Type>> childrenByParent)
    {
        var visited = new HashSet<Type>();
        var visiting = new HashSet<Type>(); // cycle detection
        var output = new List<Type>();

        void DepthFirtSearch(Type type)
        {
            if (visited.Contains(type))
            {
                return;
            }

            if (!visiting.Add(type))
            {
                throw new InvalidOperationException(
                    $"Reference cycle detected in Domain aggregate '{root.FullName}' while intializing Krud"
                        + $" - entity '{type.FullName}' has children referencing '{type.FullName}'"
                );
            }

            if (childrenByParent.TryGetValue(type, out var childrenTypes))
            {
                foreach (var childType in childrenTypes)
                {
                    DepthFirtSearch(childType);
                }
            }

            visiting.Remove(type);
            visited.Add(type);
            output.Add(type);
        }

        DepthFirtSearch(root);
        return output;
    }

    public static KrudNode CreateParentNode(Type childType)
    {
        var nodeType = typeof(KrudParentNodeOf<>).MakeGenericType(childType);
        return (KrudNode)Activator.CreateInstance(nodeType)!;
    }

    public static IEnumerable<Type> GetClosedKrudParentInterfaces(Type nodeType)
    {
        // Finds IKrudV1ParentNodeOf<T> (closed) on the node type
        return nodeType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IKrudParentNodeOf<>));
    }

    public static IEnumerable<Type> GetClosedEntityMirrorInterfaces(Type type)
    {
        return type
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityMirror<>));
    }

    public static IReadOnlyList<Type> GetEntityMirrorPrincipalTypes(IEnumerable<Type> types)
    {
        return types
            .SelectMany(GetClosedEntityMirrorInterfaces)
            .Select(i => i.GetGenericArguments()[0])
            .Where(typeof(Entity).IsAssignableFrom)
            .Distinct()
            .ToList();
    }

    public static bool TryReflect(Entity dependent, Entity principal)
    {
        var method = MIRROR_METHODS.GetOrAdd(
            (dependent.GetType(), principal.GetType()),
            static key => FindMirrorMethod(key.DependentType, key.PrincipalType)
        );
        if (method == null)
        {
            return false;
        }

        return (bool)(method.Invoke(dependent, [principal]) ?? false);
    }

    static IEnumerable<Type> UnwrapPropertyTypes(Type type)
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

    static MethodInfo? FindMirrorMethod(Type dependentType, Type principalType)
    {
        var mirrorInterface = GetClosedEntityMirrorInterfaces(dependentType)
            .OrderByDescending(i => i.GetGenericArguments()[0] == principalType)
            .FirstOrDefault(i => i.GetGenericArguments()[0].IsAssignableFrom(principalType));

        return mirrorInterface?.GetMethod(nameof(IEntityMirror<Entity>.Reflect));
    }
}
