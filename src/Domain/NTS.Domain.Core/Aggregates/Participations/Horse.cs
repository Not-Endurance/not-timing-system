using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations;

public class Horse : IHorse
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Horse(int id, string name, string? feiId)
    {
        Id = id;
        Name = name;
        FeiId = feiId;
    }

    public Horse(IHorse horse)
    {
        Id = horse.Id;
        Name = horse.Name;
        FeiId = horse.FeiId;
    }

    public string Name { get; private set; }
    public string? FeiId { get; private set; }
    public int Id { get; private set; }

    public override string ToString()
    {
        return Name;
    }
}
