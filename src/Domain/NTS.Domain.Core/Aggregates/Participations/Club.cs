using Not.Domain.Aggregates;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations;

public class Club : Entity
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Club(string name) : base(name)
    {
        Name = name;
    }

    public string Name { get; private set; }

    public override string ToString()
    {
        return Name;
    }
}
