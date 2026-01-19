using Newtonsoft.Json;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Setup.Aggregates;

public class Club : Aggregate
{
    public Club(string? name)
        : this(GenerateId(), name) { }

    [System.Text.Json.Serialization.JsonConstructor]
    [JsonConstructor]
    public Club(int? id, string? name)
        : base(id!.Value)
    {
        Name = Required(nameof(Name), name);
    }

    public string Name { get; }

    public override string ToString()
    {
        return Name;
    }
}
