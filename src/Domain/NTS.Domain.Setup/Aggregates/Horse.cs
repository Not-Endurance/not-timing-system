namespace NTS.Domain.Setup.Aggregates;

public class Horse : Aggregate
{
    public Horse(string? name, string? feiId)
        : this(GenerateId(), name, feiId) { }

    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Horse(int? id, string? name, string? feiId)
        : base(id!.Value)
    {
        Name = Required(nameof(Name), name);
        FeiId = feiId;
    }

    public string Name { get; }
    public string? FeiId { get; }

    public string Summarize()
    {
        return ToString();
    }

    public override string ToString()
    {
        return Name;
    }
}
