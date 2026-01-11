using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Nodes;
using Not.Exceptions;

namespace Not.Application.Krud.Services;

public class KrudRootProvider<T> : IKrudNodeSetter
{
    private readonly Type _rootType;

    public KrudRootProvider()
    {
        _rootType = typeof(T);
    }

    internal KrudNode? Root { get; private set; }

    public void SetParent(object aggregate)
    {
        GuardHelper.ThrowIfDefault(Root);
        if (aggregate.GetType() != _rootType)
        {
            return;
        }
        Root.Set(aggregate);
    }

    internal void SetRootNode(KrudNode node)
    {
        Root = node;
    }
}
