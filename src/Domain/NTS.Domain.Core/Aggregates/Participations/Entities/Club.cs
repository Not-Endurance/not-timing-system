namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Club : Entity
{
    public Club(int id, string name)
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
