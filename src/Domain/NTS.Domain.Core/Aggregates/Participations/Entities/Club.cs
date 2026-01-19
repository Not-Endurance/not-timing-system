namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Club : Entity
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Club(int id, string name) : base(id)
    {
        Name = name;
    }

    public string Name { get; private set; }

    public override string ToString()
    {
        return Name;
    }
}
