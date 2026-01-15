using Not.Application.Krud.Graph;
using Not.Exceptions;

namespace Not.Application.Krud.ServiceRegistration;

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

        // 4) build group nodes bottom-up
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
                // In case there is already a parent we must attach it's node and replace it with the current 'childNode'
                // in 'nodesByType'. Otherwise if the previous node is a Consolidated node (the parent has multiple children)
                // then we won't register 'IKrudParentNodeOf<T>' instance which will then cause an error.
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

        return new KrudGraph(root, allNodes.ToList(), nodesByType);
    }
}
