using Newtonsoft.Json;
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

    [System.Text.Json.Serialization.JsonConstructor]
    [JsonConstructor]
    public Club(int id, string name) : base(id)
    {
        Name = name;
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
