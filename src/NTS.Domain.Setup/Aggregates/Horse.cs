using Not.Domain.Base;

namespace NTS.Domain.Setup.Aggregates;

public class Horse : AggregateRoot, IAggregateRoot
{
    public static Horse Create(string? name, string? feiId)
    {
        return new(name, feiId);
    }

    public static Horse Update(int? id, string? name, string? feiId)
    {
        return new(id, name, feiId);
    }

    Horse(string? name, string? feiId)
        : this(GenerateId(), name, feiId) { }

    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Horse(int? id, string? name, string? feiId)
        : base(id!.Value)
    {
        Name = Required(nameof(Name), name);
        FeiId = feiId;
    }

    public string? FeiId { get; }
    public string Name { get; }

    public string Summarize()
    {
        return ToString();
    }

    public override string ToString()
    {
        return Name;
    }
}
