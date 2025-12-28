using Not.Domain.Aggregates;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations;

public class Athlete : Aggregate, IAthlete
{
    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Athlete(Person names, Country country, Club? club, string feiId)
    {
        Names = names;
        Country = country;
        Club = club;
        FeiId = feiId;
    }

    public Athlete(IAthlete athlete)
    {
        Names = athlete.Names;
        Country = athlete.Country;
        FeiId = athlete.FeiId;
    }

    public Person Names { get; }
    public Country Country { get; }
    public IClub? Club { get; }
    public string? FeiId { get; }

    public override string ToString()
    {
        return $"{Names}, {Country}";
    }
}
