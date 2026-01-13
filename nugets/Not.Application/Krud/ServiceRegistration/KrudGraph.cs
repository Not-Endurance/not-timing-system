using Not.Application.Krud.Nodes;

namespace Not.Application.Krud.ServiceRegistration;

internal sealed record KrudGraph
{
    public KrudGraph(IReadOnlyList<KrudNode> allNodes, IReadOnlyDictionary<Type, KrudNode> nodesByParentType)
    {
        AllNodes = allNodes;
        NodesByParentType = nodesByParentType;
    }

    public IReadOnlyList<KrudNode> AllNodes { get; }
    public IReadOnlyDictionary<Type, KrudNode> NodesByParentType { get; }
}
