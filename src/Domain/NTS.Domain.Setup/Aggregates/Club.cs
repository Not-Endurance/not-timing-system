namespace NTS.Domain.Setup.Aggregates;

public class Club : Aggregate
{
    public Club(string? name, int? id = null)
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
