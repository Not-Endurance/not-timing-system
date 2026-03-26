using Not.Krud.ServiceRegistration;

namespace Not.Krud.Graph;

internal sealed record KrudGraphMetadata
{
    public static KrudGraphMetadata Build(Type rootType)
    {
        var graph = KrudGraphHelper.Build(rootType);
        var rootMirrorPrincipalTypes = KrudReflectionHelper.GetEntityMirrorPrincipalTypes([rootType]);

        if (graph.IsFlatAggregate)
        {
            return new KrudGraphMetadata(true, [], [], [], rootMirrorPrincipalTypes);
        }

        var concrete = graph.AllNodes.Select(n => n.GetType()).Distinct().ToList();

        var interfaces = graph
            .AllNodes.SelectMany(n => KrudReflectionHelper.GetClosedKrudParentInterfaces(n.GetType()))
            .Distinct()
            .ToList();

        var managedEntityTypes = interfaces.Select(i => i.GetGenericArguments()[0]).Distinct().ToList();
        var graphMirrorPrincipalTypes = KrudReflectionHelper.GetEntityMirrorPrincipalTypes(managedEntityTypes);

        return new KrudGraphMetadata(
            false,
            concrete,
            interfaces,
            graphMirrorPrincipalTypes,
            rootMirrorPrincipalTypes
        );
    }

    public KrudGraphMetadata(
        bool isFlatAggregate,
        IReadOnlyList<Type> concreteNodeTypes,
        IReadOnlyList<Type> krudParentNodeOfClosedInterfaces,
        IReadOnlyList<Type> graphMirrorPrincipalTypes,
        IReadOnlyList<Type> rootMirrorPrincipalTypes
    )
    {
        IsFlatAggregate = isFlatAggregate;
        ConcreteNodeTypes = concreteNodeTypes;
        KrudParentNodeOfClosedInterfaces = krudParentNodeOfClosedInterfaces;
        GraphMirrorPrincipalTypes = graphMirrorPrincipalTypes;
        RootMirrorPrincipalTypes = rootMirrorPrincipalTypes;
    }

    public bool IsFlatAggregate { get; }
    public IReadOnlyList<Type> ConcreteNodeTypes { get; }
    public IReadOnlyList<Type> KrudParentNodeOfClosedInterfaces { get; }
    public IReadOnlyList<Type> GraphMirrorPrincipalTypes { get; }
    public IReadOnlyList<Type> RootMirrorPrincipalTypes { get; }
}
