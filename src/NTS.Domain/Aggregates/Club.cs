using Not.Domain.Base;

namespace NTS.Domain.Aggregates;

public class Club : AggregateRoot
{
    public static Club? Create(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        return new Club(name);
    }

    public Club(string name) : base(GenerateId())
    {
        Name = name;
    }

    public string Name { get; }

    public override string ToString()
    {
        return Name;
    }
}
