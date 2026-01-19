namespace NTS.Domain.Setup.Aggregates;

public class Club : Aggregate
{
    public Club(int? id, string? name)
        : base(id)
    {
        Name = Required(nameof(Name), name);
    }

    public string Name { get; }

    public override string ToString()
    {
        return Name;
    }
}
