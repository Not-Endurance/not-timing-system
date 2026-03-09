using System.Diagnostics.CodeAnalysis;

namespace Not.Krud.Graph;

internal sealed record KrudGraph
{
    public KrudGraph(
        KrudNode? root,
        IReadOnlyList<KrudNode> allNodes,
        IReadOnlyDictionary<Type, KrudNode> nodesByParentType,
        IReadOnlyDictionary<Type, IReadOnlyList<KrudDependency>> dependenciesByPrincipalType
    )
    {
        IsFlatAggregate = root == null;
        Root = root;
        AllNodes = allNodes;
        NodesByParentType = nodesByParentType;
        DependenciesByPrincipalType = dependenciesByPrincipalType;
    }

    [MemberNotNullWhen(false, nameof(Root))]
    public bool IsFlatAggregate { get; }
    public KrudNode? Root { get; }
    public IReadOnlyList<KrudNode> AllNodes { get; }
    public IReadOnlyDictionary<Type, KrudNode> NodesByParentType { get; }
    public IReadOnlyDictionary<Type, IReadOnlyList<KrudDependency>> DependenciesByPrincipalType { get; }
}
