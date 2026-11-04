using Not.Domain;
using Not.Krud.Graph;

namespace Not.Krud.ServiceRegistration;

internal static class KrudGraphHelper
{
    public static KrudGraph Build(Type rootType)
    {
        var entityTypes = KrudReflectionHelper.GetEntityTypesIn(rootType);

        var childrenByParent = entityTypes.ToDictionary(
            t => t,
            t => KrudReflectionHelper.GetEdgeAggregatesOf(t).Where(entityTypes.Contains).Distinct().ToList()
        );

        var ordered = KrudReflectionHelper.OrderUsingDepthFirstSearch(rootType, childrenByParent);

        var nodesByType = new Dictionary<Type, KrudNode>();
        var allNodes = new HashSet<KrudNode>(new ReferenceEqualityComparer<KrudNode>());
        KrudNode? root = null;
        foreach (var parentType in ordered)
        {
            var childTypes = childrenByParent[parentType];

            var isLeaf = childTypes.Count == 0;
            if (isLeaf)
            {
                continue;
            }

            var parentNodes = new List<KrudNode>(childTypes.Count);
            foreach (var childType in childTypes)
            {
                var childNode = KrudReflectionHelper.CreateParentNode(childType);
                if (nodesByType.TryGetValue(childType, out var previousChildNode))
                {
                    childNode.AttachChildren([previousChildNode]);
                }

                parentNodes.Add(childNode);
                allNodes.Add(childNode);
            }

            KrudNode parentNode;
            if (parentNodes.Count == 1)
            {
                parentNode = parentNodes[0];
            }
            else
            {
                parentNode = new KrudNode();
                parentNode.AttachChildren(parentNodes);
                allNodes.Add(parentNode);
            }

            if (parentType == rootType)
            {
                root = parentNode;
            }

            nodesByType[parentType] = parentNode;
        }

        var managedEntityTypes = allNodes
            .SelectMany(node => KrudReflectionHelper.GetClosedKrudParentInterfaces(node.GetType()))
            .Select(@interface => @interface.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        var dependenciesByPrincipalType = BuildDependencies(childrenByParent, managedEntityTypes);

        return new KrudGraph(root, allNodes.ToList(), nodesByType, dependenciesByPrincipalType);
    }

    static IReadOnlyDictionary<Type, IReadOnlyList<KrudDependency>> BuildDependencies(
        IReadOnlyDictionary<Type, List<Type>> childrenByParent,
        IReadOnlyCollection<Type> managedEntityTypes
    )
    {
        var managedSet = managedEntityTypes.ToHashSet();
        var dependencies = new List<KrudDependency>();

        foreach (var dependentType in managedSet)
        {
            var parentTypes = childrenByParent
                .Where(x => x.Value.Contains(dependentType))
                .Select(x => x.Key)
                .Distinct()
                .ToList();
            if (!parentTypes.Any())
            {
                continue;
            }

            var properties = dependentType.GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
            );
            foreach (var property in properties)
            {
                var principalType = property.PropertyType;
                if (principalType == dependentType)
                {
                    continue;
                }

                if (!typeof(Entity).IsAssignableFrom(principalType))
                {
                    continue;
                }

                foreach (var parentType in parentTypes)
                {
                    dependencies.Add(
                        new KrudDependency(
                            principalType,
                            parentType,
                            dependentType,
                            property,
                            $"{dependentType.Name}.{property.Name}"
                        )
                    );
                }
            }
        }

        return dependencies
            .GroupBy(x => x.PrincipalType)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<KrudDependency>)x.ToList());
    }
}
