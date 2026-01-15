using Not.Application.Krud.ServiceRegistration;

namespace Not.Application.Krud.Graph;

internal sealed record KrudGraphMetadata(
    bool IsFlatAggregate,
    IReadOnlyList<Type> ConcreteNodeTypes,
    IReadOnlyList<Type> KrudParentNodeOfClosedInterfaces)
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
}
