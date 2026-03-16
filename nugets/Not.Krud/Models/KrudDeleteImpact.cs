namespace Not.Krud.Models;

public sealed record KrudDeleteImpact
{
    public KrudDeleteImpact(string target, IReadOnlyList<KrudDeleteUsage> usages)
    {
        Target = target;
        Usages = usages;
    }

    public string Target { get; }
    public IReadOnlyList<KrudDeleteUsage> Usages { get; }
    public bool HasUsages => Usages.Count > 0;
}

public sealed record KrudDeleteUsage
{
    public KrudDeleteUsage(string relation, string owner, string dependent)
    {
        Relation = relation;
        Owner = owner;
        Dependent = dependent;
    }

    public string Relation { get; }
    public string Owner { get; }
    public string Dependent { get; }
}
