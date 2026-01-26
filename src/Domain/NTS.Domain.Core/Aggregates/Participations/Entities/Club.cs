namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Club : Entity
{
    public Club(string name, int id)
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
