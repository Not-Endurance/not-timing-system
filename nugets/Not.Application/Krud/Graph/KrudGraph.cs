using System.Diagnostics.CodeAnalysis;

namespace Not.Application.Krud.Graph;

internal sealed record KrudGraph
{
    public KrudGraph(
        KrudNode? root,
        IReadOnlyList<KrudNode> allNodes,
        IReadOnlyDictionary<Type, KrudNode> nodesByParentType
    )
    {
        IsFlatAggregate = root == null;
        Root = root;
        AllNodes = allNodes;
        NodesByParentType = nodesByParentType;
    }

    [MemberNotNullWhen(false, nameof(Root))]
    public bool IsFlatAggregate { get; }
    public KrudNode? Root { get; }
    public IReadOnlyList<KrudNode> AllNodes { get; }
    public IReadOnlyDictionary<Type, KrudNode> NodesByParentType { get; }
}
