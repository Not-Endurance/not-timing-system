namespace NTS.Domain.Setup.Aggregates;

public class Club : Aggregate
{
    public Club(string? name, Guid? id = null)
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
