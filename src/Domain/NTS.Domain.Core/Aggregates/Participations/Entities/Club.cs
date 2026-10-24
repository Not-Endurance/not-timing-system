namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Club : Entity
{
    public Club(string name, Guid id)
        : base(id)
    {
        Name = name;
    }

    public string Name { get; private set; }

    public override string ToString()
    {
        return Name;
    }
}
