using Newtonsoft.Json;
using Not.Domain.Base;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Setup.Aggregates;

public class Club : AggregateRoot, IClub, IAggregateRoot
{
    public static Club Create(string? name)
    {
        return new Club(name);
    }

    public static Club Update(int id, string? name)
    {
        return new Club(id, name);
    }

    public Club(string? name)
        : this(GenerateId(), name) { }

    [System.Text.Json.Serialization.JsonConstructor]
    [JsonConstructor]
    public Club(int id, string? name)
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
