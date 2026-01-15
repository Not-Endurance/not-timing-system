using Not.Application.Krud.ServiceRegistration;

namespace Not.Application.Krud.Graph;

internal sealed record KrudGraphMetadata
{
    public static KrudGraphMetadata Build(Type rootType)
    {
        var graph = KrudGraphHelper.Build(rootType);

        if (graph.IsFlatAggregate)
        {
            return new KrudGraphMetadata(true, [], []);
        }

        var concrete = graph.AllNodes
            .Select(n => n.GetType())
            .Distinct()
            .ToList();

        var interfaces = graph.AllNodes
            .SelectMany(n => KrudReflectionHelper.GetClosedKrudParentInterfaces(n.GetType()))
            .Distinct()
            .ToList();

        return new KrudGraphMetadata(false, concrete, interfaces);
    }

    public KrudGraphMetadata(
        bool isFlatAggregate,
        IReadOnlyList<Type> concreteNodeTypes,
        IReadOnlyList<Type> krudParentNodeOfClosedInterfaces)
    {
        IsFlatAggregate = isFlatAggregate;
        ConcreteNodeTypes = concreteNodeTypes;
        KrudParentNodeOfClosedInterfaces = krudParentNodeOfClosedInterfaces;
    }

    public bool IsFlatAggregate { get; }
    public IReadOnlyList<Type> ConcreteNodeTypes { get; }
    public IReadOnlyList<Type> KrudParentNodeOfClosedInterfaces { get; }
}
