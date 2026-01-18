using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations;

public class Horse : IHorse
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Horse(string name, string? feiId)
    {
        Name = name;
        FeiId = feiId;
    }

    public Horse(IHorse horse)
    {
        Name = horse.Name;
        FeiId = horse.FeiId;
    }

    public string Name { get; private set; }
    public string? FeiId { get; private set; }

    public override string ToString()
    {
        return Name;
    }
}
