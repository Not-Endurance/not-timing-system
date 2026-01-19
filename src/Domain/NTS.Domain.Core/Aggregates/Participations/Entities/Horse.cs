namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Horse : Entity
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Horse(int id, string name, string? feiId) : base(id)
    {
        Name = name;
        FeiId = feiId;
    }

    public string Name { get; private set; }
    public string? FeiId { get; private set; }

    public override string ToString()
    {
        return Name;
    }
}
