namespace NTS.Domain.Setup.Aggregates;

public class Horse : Aggregate
{
    public Horse(string? name, string? feiId, int? id = null)
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
