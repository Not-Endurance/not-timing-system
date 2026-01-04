using Not.Domain.Aggregates;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations;

public class Club : Aggregate, IClub
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Club(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public Club(IClub club)
    {
        Id = club.Id;
        Name = club.Name;
    }

    public int Id { get; private set; }
    public string Name { get; private set; }

    public override string ToString()
    {
        return Name;
    }
}
