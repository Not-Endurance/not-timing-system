using System.Reflection;

namespace Not.Krud.Graph;

internal sealed class KrudDependency
{
    public KrudDependency(
        Type principalType,
        Type parentType,
        Type dependentType,
        PropertyInfo property,
        string relation
    )
    {
        PrincipalType = principalType;
        ParentType = parentType;
        DependentType = dependentType;
        Property = property;
        Relation = relation;
    }

    public Type PrincipalType { get; }
    public Type ParentType { get; }
    public Type DependentType { get; }
    public PropertyInfo Property { get; }
    public string Relation { get; }
}
