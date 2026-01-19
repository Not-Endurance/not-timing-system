namespace NTS.Domain.Setup.Aggregates;

public class Horse : Aggregate
{
    public Horse(int? id, string? name, string? feiId)
        : base(id)
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
